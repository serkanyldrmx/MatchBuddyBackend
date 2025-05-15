using MatchBuddy.Api.Model;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Business.Constants;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.DataAccess;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatchBuddy.Business.Concrete
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
            return new SuccessResult("Bildirim eklendi.");
        }

        public IResult Delete(int notificationId)
        {
            var notification = _notificationDal.Get(n => n.NotificationId == notificationId);
            if (notification == null)
            {
                return new ErrorResult("Bildirim bulunamadı.");
            }

            _notificationDal.Delete(notification);
            return new SuccessResult("Bildirim silindi.");
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
            using (var context = new MatchBuddyContext())
            {
                var notifications = context.PlayerNotification
                    .Where(pn => pn.PlayerId == playerId )
                    .Include(pn => pn.Notification)
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
            return new SuccessResult("Bildirim güncellendi.");
        }

        public IResult MarkAsRead(int playerNotificationId)
        {
            var pn = _playerNotificationDal.Get(p => p.PlayerNotificationId == playerNotificationId);
            if (pn == null)
            {
                return new ErrorResult("Bildirim bulunamadı.");
            }

            pn.IsRead = true;
            _playerNotificationDal.Update(pn);
            return new SuccessResult("Bildirim okundu olarak işaretlendi.");
        }

        public IResult SendNotificationToPlayers(List<int> playerIds, Notification notification)
        {
            notification.CreatedAt = DateTime.Now;
            _notificationDal.Add(notification);

            foreach (var playerId in playerIds)
            {
                _playerNotificationDal.Add(new PlayerNotification
                {
                    NotificationId = notification.NotificationId,
                    PlayerId = playerId,
                    IsRead = false,
                    SentAt = DateTime.Now
                });
            }

            return new SuccessResult("Bildirim oyunculara gönderildi.");
        }
    }

}
