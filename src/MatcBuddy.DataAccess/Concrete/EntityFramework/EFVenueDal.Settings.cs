using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    /// <summary>Tesis düzeyi işletme ayarları (VenueSettings) — docs/platform-api.md.</summary>
    public partial class EFVenueDal
    {
        private static VenueSettingsDto SettingsDto(MatcBuddyContext context, int stadiumId)
        {
            var row = context.VenueSettings.AsNoTracking().FirstOrDefault(x => x.StadiumId == stadiumId);
            return new VenueSettingsDto
            {
                StadiumId = stadiumId,
                MinBookingNoticeHours = row?.MinBookingNoticeHours,
                FreeCancellationHours = row?.FreeCancellationHours,
                MaxAdvanceBookingDays = row?.MaxAdvanceBookingDays,
                Effective = VenueRules.EffectiveSettings(context, stadiumId),
                Defaults = VenueRules.DefaultSettings(),
                UpdatedAt = row?.UpdatedAt,
            };
        }

        public OperationResult<VenueSettingsDto> GetSettings(int me, int stadiumId)
        {
            using var context = new MatcBuddyContext();
            var g = Guard(context, me, StadiumOf(context, stadiumId), write: false);
            if (g != null) return Fail<VenueSettingsDto>(g);
            return OperationResult<VenueSettingsDto>.Ok(SettingsDto(context, stadiumId));
        }

        public OperationResult<VenueSettingsDto> SaveSettings(int me, VenueSettingsInput input)
        {
            using var context = new MatcBuddyContext();
            var g = Guard(context, me, StadiumOf(context, input.StadiumId), write: true);
            if (g != null) return Fail<VenueSettingsDto>(g);

            var row = context.VenueSettings.FirstOrDefault(x => x.StadiumId == input.StadiumId);
            if (row == null)
            {
                row = new VenueSettings { StadiumId = input.StadiumId };
                context.VenueSettings.Add(row);
            }
            row.MinBookingNoticeHours = input.MinBookingNoticeHours;
            row.FreeCancellationHours = input.FreeCancellationHours;
            row.MaxAdvanceBookingDays = input.MaxAdvanceBookingDays;
            row.UpdatedAt = DateTime.Now;
            row.UpdatedByPlayerId = me;
            context.SaveChanges();
            return OperationResult<VenueSettingsDto>.Ok(SettingsDto(context, input.StadiumId), Msg.Get(MessageKeys.Common.Saved));
        }
    }
}
