using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AccuratPanelCWD.Services
{
    /// <summary>
    /// Конвенция v2 на краю клиента: провод несёт абсолютные моменты (UTC),
    /// UI живёт в локальном настенном времени. Конвертация — только здесь.
    /// </summary>
    public class UtcLocalDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToSerialize, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s)) return default;
                var dt = DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                return dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
            }
            return reader.GetDateTime().ToLocalTime();
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
            writer.WriteStringValue(utc.ToString("O", CultureInfo.InvariantCulture));
        }
    }

    public static class JsonOpts
    {
        public static readonly JsonSerializerOptions Default = Create();

        private static JsonSerializerOptions Create()
        {
            var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            o.Converters.Add(new UtcLocalDateTimeConverter());
            return o;
        }
    }

    /// <summary>Обёртки над System.Net.Http.Json с нашими опциями (единая точка конвертации).</summary>
    public static class HttpJsonExtensions
    {
        public static Task<T?> GetJsonAsync<T>(this HttpClient http, string url) =>
            http.GetFromJsonAsync<T>(url, JsonOpts.Default);

        public static Task<T?> ReadJsonAsync<T>(this HttpContent content) =>
            content.ReadFromJsonAsync<T>(JsonOpts.Default);

        public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient http, string url, object payload) =>
            http.PostAsJsonAsync(url, payload, JsonOpts.Default);

        public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient http, string url, object payload) =>
            http.PutAsJsonAsync(url, payload, JsonOpts.Default);

        public static Task<HttpResponseMessage> PatchJsonAsync(this HttpClient http, string url, object payload) =>
            http.PatchAsJsonAsync(url, payload, JsonOpts.Default);
    }
}