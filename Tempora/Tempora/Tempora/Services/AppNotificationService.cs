using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace Tempora.Services
{
    public class AppNotificationService : INotificationService
    {
        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        public void Show(string title, string body)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                var toast = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(body)
                    .BuildNotification();

                AppNotificationManager.Default.Show(toast); // COM-bound, needs UI thread
            });
        }
    }
}
