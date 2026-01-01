using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace CommonDatabase.DTO
{
    public class ClientAuth
    {
        [Required]
        public string Username { get; set; }
        [Required]
        public string Password { get; set; }
        [Required]
        public string DeviceId { get; set; }
        [Required]
        public string DeviceToken { get; set; }
        [Required]
        public string DeviceType { get; set; }
    }

    public class ClientDevices
    {
        [Key]
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string DeviceId { get; set; }
        public string DeviceToken { get; set; }
        public string DeviceType { get; set; }
        public DateTime? LastLogin { get; set; }
        public bool IsLogout { get; set; }  
        public bool IsActive { get; set; }
        public bool IsDND { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }

    [Keyless]
    public class DeviceAccessRawDto
    {
        public int ClientId { get; set; }
        public string DeviceToken { get; set; }
        public string DeviceId { get; set; }
        public string DeviceType { get; set; }
        public bool IsDND { get; set; }
        public bool HasNewsAccess { get; set; }      // 👈 Use int
        public bool HasRateAccess { get; set; }      // 👈 Use int
        public DateTime NewsExpirationDate { get; set; }
        public DateTime RateExpirationDate { get; set; }
    }
    public class ClientDto
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public bool IsActive { get; set; }
        public DateTime NewsExpireDate { get; set; }
        public DateTime RateExpireDate { get; set; }
        public string Topics { get; set; }
        public string Keywords { get; set; }
        public List<DeviceAccessDto> DeviceAccess { get; set; }
    }
    public class DeviceAccessDto
    {
        public string DeviceToken { get; set; }
        public string DeviceType { get; set; }
        public string DeviceId { get; set; }
        public bool IsDND { get; set; }
        public bool HasNewsAccess { get; set; }
        public bool HasRateAccess { get; set; }
    }

    public class DeviceNotificationDto
    {
        public string Username { get; set; }
        public string DeviceToken { get; set; }
        public string DeviceType { get; set; }
        public string DeviceId { get; set; }
        public bool IsDND { get; set; }
        public bool HasNewsAccess { get; set; }
        public bool HasRateAccess { get; set; }
    }
    public class ClientAccessModel
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public int AccessNoOfNews { get; set; }
        public int AccessNoOfRate { get; set; }
        public string ClientName { get; set; }
        public string? DeviceToken { get; set; }
        public bool IsActive { get; set; }
        public bool IsNews { get; set; }
        public bool IsRate { get; set; }
        public DateTime NewsExpiredDate { get; set; }
        public DateTime RateExpiredDate { get; set; }
        public string Topics { get; set; }
        public string Keywords { get; set; }
    }
    public class LogoutRequest
    {
        //public string Token { get; set; }
        public int UserId { get; set; }
        public string DeviceId { get; set; }
    }
    public class TopicKeyword
    {
        //public string Token { get; set; }
        public int UserId { get; set; }
        public bool IsTopic { get; set; }
        public string TopicOrKeyword { get; set; }
    }

    public class StatusDnd
    {
        //public string Token { get; set; }
        public int UserId { get; set; }
        public string DeviceId { get; set; }
        public bool IsDND { get; set; }
    }
    [Keyless]
    public class ClientWiseInstrumentList
    {
        //public string Token { get; set; }
        public string Username { get; set; }
        public string Identifier { get; set; }
        public string Contract { get; set; }
        public long RowId { get; set; }
    }
    public enum DeviceType
    {
        android,
        ios,
        desktop,
        web
    }

}
