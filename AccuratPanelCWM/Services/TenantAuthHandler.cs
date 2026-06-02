using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace AccuratPanelCWM.Services
{
    public class TenantAuthHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Достаем сохраненный ID компании (если он есть)
            var companyId = Preferences.Default.Get("CompanyId", 0);

            if (companyId > 0)
            {
                // Важно: если заголовок уже есть (например, повторный запрос), удаляем старый
                if (request.Headers.Contains("X-Company-Id"))
                {
                    request.Headers.Remove("X-Company-Id");
                }

                request.Headers.Add("X-Company-Id", companyId.ToString());
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}