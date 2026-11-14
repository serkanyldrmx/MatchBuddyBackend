using MatcBuddy.Api.Model;
using MatcBuddy.Business.Abstract;
using MatcBuddy.Business.Constants;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatcBuddy.Business.Concrete
{
    public class NotificationManager : INotificationService
    {
        private readonly INotificationDal _notificationDal;
        private readonly IPlayerNotificationDal _playerNotificationDal;

        public NotificationManager(INotificationDal notificationDal, IPlayerNotificationDal playerNotificationDal)
        {
            _notificationDal = notificationDal;
            _playerNotificationDal = playerNotificationDal;
        }

        public IResult Add(Notification notification)
        {
            notification.CreatedAt = DateTime.Now;
            _notificationDal.Add(notification);
            return new SuccessResult(Msg.Get(MessageKeys.Notification.Added));
        }

        public IResult Delete(int notificationId)
        {
            var notification = _notificationDal.Get(n => n.NotificationId == notificationId);
            if (notification == null)
            {
                return new ErrorResult(ErrorCodes.Notification.NotFound);
            }

            _notificationDal.Delete(notification);
            return new SuccessResult(Msg.Get(MessageKeys.Notification.Deleted));
        }

        public IDataResult<List<Notification>> GetAllNotifications()
        {
            return new SuccessDataResult<List<Notification>>(_notificationDal.GetAll());
        }

        public IDataResult<Notification> GetById(int notificationId)
        {
            return new SuccessDataResult<Notification>(_notificationDal.Get(n => n.NotificationId == notificationId));
        }

        public IDataResult<List<PlayerNotificationDto>> GetUnreadNotificationsByPlayerId(int playerId)
        {
            using (var context = new MatcBuddyContext())
            {
                var notifications = context.PlayerNotification
                    .Where(pn => pn.PlayerId == playerId)
                    .Include(pn => pn.Notification)
                    .OrderByDescending(pn => pn.Notification.CreatedAt)
                    .ThenByDescending(pn => pn.PlayerNotificationId)
                    .Take(100)
                    .Select(pn => new PlayerNotificationDto
                    {
                        PlayerNotificationId = pn.PlayerNotificationId,
                        IsRead = pn.IsRead,
                        Notification = pn.Notification
                    })
                    .ToList();

                return new SuccessDataResult<List<PlayerNotificationDto>>(notifications);
            }
        }



        public IResult Update(Notification notification)
        {
            _notificationDal.Update(notification);
            return new SuccessResult(Msg.Get(MessageKeys.Notification.Updated));
        }

        public IResult MarkAsRead(int playerNotificationId, int playerId)
        {
            // Sadece bildirimin sahibi okundu olarak işaretleyebilir
            var pn = _playerNotificationDal.Get(p => p.PlayerNotificationId == playerNotificationId && p.PlayerId == playerId);
            if (pn == null)
            {
                return new ErrorResult(ErrorCodes.Notification.NotFound);
            }

            pn.IsRead = true;
            _playerNotificationDal.Update(pn);
            return new SuccessResult(Msg.Get(MessageKeys.Notification.MarkedRead));
        }

        public IResult MarkAllAsRead(int playerId)
        {
            using var context = new MatcBuddyContext();
            context.PlayerNotification
                .Where(p => p.PlayerId == playerId && !p.IsRead)
                .ExecuteUpdate(s => s.SetProperty(p => p.IsRead, true));
            return new SuccessResult(Msg.Get(MessageKeys.Notification.MarkedRead));
        }

        public IResult SendNotificationToPlayers(List<int> playerIds, Notification notification)
        {
            // Alıcı tercihleri (PlayerSettings) ve silinmiş hesaplar NotificationWriter'da süzülür
            _notificationDal.SendToPlayers(notification.Type, notification.Text, notification.MatchId,
                notification.StadiumId, playerIds ?? new List<int>());

            return new SuccessResult(Msg.Get(MessageKeys.Notification.Sent));
        }
    }

}
