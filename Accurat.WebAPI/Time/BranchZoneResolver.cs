using Accurat.WebAPI.Data;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using System.Collections.Concurrent;

namespace Accurat.WebAPI.Time
{
    /// <summary>
    /// Разрешает часовую зону филиала по IANA-идентификатору.
    /// </summary>
    public interface IBranchZoneResolver
    {
        DateTimeZone DefaultZone { get; }
        DateTimeZone GetZone(int branchId);
        DateTimeZone GetZone(string? timeZoneId);
    }

    public sealed class BranchZoneResolver : IBranchZoneResolver
    {
        /// <summary>Зона по умолчанию для филиалов без явной настройки.</summary>
        public const string DefaultZoneId = "Europe/Moscow";

        private static readonly ConcurrentDictionary<string, DateTimeZone> Cache = new();
        private readonly AppDbContext _db;

        public BranchZoneResolver(AppDbContext db) => _db = db;

        public DateTimeZone DefaultZone => GetZone(null);

        public DateTimeZone GetZone(int branchId)
        {
            var zoneId = _db.Branches.AsNoTracking()
                .Where(b => b.Id == branchId)
                .Select(b => b.TimeZoneId)
                .FirstOrDefault();
            return GetZone(zoneId);
        }

        public DateTimeZone GetZone(string? timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId)) timeZoneId = DefaultZoneId;
            return Cache.GetOrAdd(timeZoneId, id =>
                DateTimeZoneProviders.Tzdb.GetZoneOrNull(id)
                ?? DateTimeZoneProviders.Tzdb[DefaultZoneId]);
        }
    }
}