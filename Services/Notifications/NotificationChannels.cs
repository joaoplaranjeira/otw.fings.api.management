using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using otw.fings.api.management.Infrastructure.Data;
using otw.fings.api.management.Settings;
using WebPush;

namespace otw.fings.api.management.Services.Notifications;

public sealed class InAppNotificationChannel : INotificationChannel
{
    public NotificationChannelType ChannelName => NotificationChannelType.InApp;
    public Task<DeliveryResult> SendAsync(Notification notification, NotificationDelivery delivery, CancellationToken cancellationToken) =>
        Task.FromResult(new DeliveryResult(true));
}

public sealed class WebPushNotificationChannel(
    FingsDbContext dbContext,
    IOptions<WebPushSettings> options) : INotificationChannel
{
    public NotificationChannelType ChannelName => NotificationChannelType.WebPush;

    public async Task<DeliveryResult> SendAsync(Notification notification, NotificationDelivery delivery, CancellationToken cancellationToken)
    {
        if (!delivery.DestinationId.HasValue) return new DeliveryResult(false, Error: "Destino Web Push em falta.");
        var destination = await dbContext.PushSubscriptions.SingleOrDefaultAsync(x => x.Id == delivery.DestinationId, cancellationToken);
        if (destination is null || !destination.IsActive) return new DeliveryResult(false, DestinationInvalid: true, Error: "Subscrição inativa.");
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.PublicKey) || string.IsNullOrWhiteSpace(settings.PrivateKey))
            return new DeliveryResult(false, IsTransient: true, Error: "Web Push não está configurado.");

        var payload = JsonSerializer.Serialize(new
        {
            notification.Title,
            notification.Body,
            icon = "/icon-192.png",
            badge = "/icon-192.png",
            url = notification.ActionUrl,
            tag = notification.DeduplicationKey
        });
        try
        {
            using var client = new WebPushClient();
            await client.SendNotificationAsync(
                new WebPush.PushSubscription(destination.Endpoint, destination.P256dh, destination.Auth),
                payload,
                new VapidDetails(settings.Subject, settings.PublicKey, settings.PrivateKey),
                cancellationToken);
            destination.LastUsedAtUtc = DateTimeOffset.UtcNow;
            return new DeliveryResult(true);
        }
        catch (WebPushException exception)
        {
            var status = exception.StatusCode;
            var invalid = status is HttpStatusCode.NotFound or HttpStatusCode.Gone;
            var transient = status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500;
            if (invalid) destination.IsActive = false;
            return new DeliveryResult(false, transient, invalid, Error: $"Fornecedor Web Push devolveu HTTP {(int)status}.");
        }
        catch (HttpRequestException exception)
        {
            return new DeliveryResult(false, IsTransient: true, Error: exception.Message);
        }
    }
}
