using MatcBuddy.Core.Entities;

namespace MatcBuddy.Entities.Entity
{
    public class Stadium : IEntity
    {
        public int StadiumId { get; set; }
        public string? StadiumName { get; set; }
        public string? Location { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? Address { get; set; }
        public TimeOnly? OpeningTime { get; set; }
        public TimeOnly? ClosingTime { get; set; }
        public string? Description { get; set; }

        // Anlaşmalı tesis alanları (docs/venues-api.md)
        public int? OwnerPlayerId { get; set; }
        public string? Phone { get; set; }
        public bool IsListed { get; set; } = true;
        public bool CleatRental { get; set; }
        public decimal? CleatRentalPrice { get; set; }
        public bool HasCamera { get; set; }
        public decimal? CameraPrice { get; set; }
        public bool HasShower { get; set; }
        public bool HasLockerRoom { get; set; }
        public bool HasParking { get; set; }
        public bool HasCafe { get; set; }
        public bool HasShuttle { get; set; }
        public bool ProvidesBibs { get; set; }
        public bool ProvidesBall { get; set; }
        public string? CancellationPolicy { get; set; }
        public long? OsmId { get; set; }

        // Harici (anlaşmasız) saha: oyuncunun telefonla rezerve ettiği, sistemde üyesi olmayan bir yer.
        // Oyunculara tesis olarak listelenmez, işletmesi / sahası / fiyat kuralı yoktur (docs/external-venues-api.md).
        public bool IsExternal { get; set; }
        // Harici sahayı ilk kez ekleyen oyuncu (sonraki maçlarda tekrar seçebilsin diye)
        public int? CreatedByPlayerId { get; set; }
        // Admin bu harici sahayı sistemdeki bir tesise bağladıysa o tesis (satış listesinden düşer; maçları taşınır)
        public int? LinkedStadiumId { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<Match> Matches { get; set; }
    }
}
