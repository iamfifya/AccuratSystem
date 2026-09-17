// === ЯВНЫЕ АЛИАСЫ ДЛЯ КОНТРАКТНЫХ МОДЕЛЕЙ (чтобы избежать конфликтов с UI-моделями) ===
// === UI-МОДЕЛИ (без алиасов, так как они в том же неймспейсе) ===
using AccuratPanelCWD.Models;
using AccuratPanelCWD.Services;
using AccuratSystem.Contracts.DTOs;
using AccuratSystem.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ContractsBranch = AccuratSystem.Contracts.Models.Branch;
using ContractsCashboxSummary = AccuratSystem.Contracts.Models.CashboxSummary;
using ContractsClient = AccuratSystem.Contracts.Models.Client;
using ContractsEmployeeSchedule = AccuratSystem.Contracts.Models.EmployeeSchedule;
using ContractsOrder = AccuratSystem.Contracts.Models.Order;
using ContractsOrderWasher = AccuratSystem.Contracts.Models.OrderWasher;
using ContractsService = AccuratSystem.Contracts.Models.Service;
using ContractsShift = AccuratSystem.Contracts.Models.Shift;
using ContractsShiftReport = AccuratSystem.Contracts.Models.ShiftReport;
using ContractsTransaction = AccuratSystem.Contracts.Models.Transaction;
using ContractsUser = AccuratSystem.Contracts.Models.User;

namespace AccuratPanelCWD.Services
{
    public class ApiService
    {
        private static readonly HttpClient _http;

        // Статический конструктор для инициализации HttpClient
        static ApiService()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true;

            // Для работы через туннель
            _http = new HttpClient(handler) { BaseAddress = new Uri("https://cp4zpdt4-7165.uks1.devtunnels.ms/api/") };
            // Для работы через локалхост  
            // _http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7165/api/") };
        }

        public ApiService()
        {
            // Обычный конструктор теперь пустой
        }

        /// <summary>
        /// Безопасно форматирует DateTime для query-параметров URL.
        /// Использует Uri.EscapeDataString, чтобы символы типа + и : в offset не ломали парсинг.
        /// </summary>
        private static string Q(DateTime dt) => Uri.EscapeDataString(dt.ToString("O"));

        #region СМЕНЫ (SHIFTS)
        public async Task<List<ContractsShift>> GetShiftsAsync()
        {
            try { return await _http.GetJsonAsync<List<ContractsShift>>("Shifts") ?? new List<ContractsShift>(); }
            catch (HttpRequestException ex) { throw new Exception($"Смены (Shifts): {ex.Message}"); }
        }

        public async Task<ContractsShift> OpenShiftAsync(ContractsShift shift)
        {
            var response = await _http.PostJsonAsync("Shifts", shift);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadJsonAsync<ContractsShift>();
        }

        public async Task CloseShiftAsync(int id)
        {
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"Shifts/{id}/close");
            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        public async Task<ContractsShift> GetCurrentOpenShiftAsync()
        {
            var shifts = await GetShiftsAsync();
            return shifts.FirstOrDefault(s => !s.IsClosed);
        }
        #endregion

        #region УСЛУГИ (SERVICES)
        public async Task<List<ContractsService>> GetServicesAsync()
        {
            try { return await _http.GetJsonAsync<List<ContractsService>>("Services") ?? new List<ContractsService>(); }
            catch (HttpRequestException ex) { throw new Exception($"Услуги (Services): {ex.Message}"); }
        }

        public async Task<ContractsService> CreateServiceAsync(ContractsService service)
        {
            var response = await _http.PostJsonAsync("Services", service);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadJsonAsync<ContractsService>();
        }

        public async Task UpdateServiceAsync(ContractsService service)
        {
            var response = await _http.PutJsonAsync($"Services/{service.Id}", service);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteServiceAsync(int id)
        {
            var response = await _http.DeleteAsync($"Services/{id}");
            response.EnsureSuccessStatusCode();
        }
        #endregion

        #region СОТРУДНИКИ (USERS)
        public async Task<List<ContractsUser>> GetUsersAsync()
        {
            try { return await _http.GetJsonAsync<List<ContractsUser>>("Users") ?? new List<ContractsUser>(); }
            catch (HttpRequestException ex) { throw new Exception($"Сотрудники (Users): {ex.Message}"); }
        }

        public async Task<ContractsUser> CreateUserAsync(ContractsUser user)
        {
            var response = await _http.PostJsonAsync("Users", user);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadJsonAsync<ContractsUser>();
        }

        public async Task UpdateUserAsync(ContractsUser user)
        {
            var response = await _http.PutJsonAsync($"Users/{user.Id}", user);
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<Role>> GetRolesAsync()
        {
            try { return await _http.GetJsonAsync<List<Role>>("Roles") ?? new List<Role>(); }
            catch (HttpRequestException ex) { throw new Exception($"Ошибка получения должностей: {ex.Message}"); }
        }

        public async Task<List<AccuratSystem.Contracts.Models.PaymentMethod>> GetPaymentMethodsAsync(int branchId)
        {
            try
            {
                return await _http.GetJsonAsync<List<AccuratSystem.Contracts.Models.PaymentMethod>>($"PaymentMethods/by-branch/{branchId}")
                       ?? new List<AccuratSystem.Contracts.Models.PaymentMethod>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки способов оплаты: {ex.Message}");
                return new List<AccuratSystem.Contracts.Models.PaymentMethod>();
            }
        }

        public async Task<List<AccuratSystem.Contracts.Models.OrderStatuses>> GetOrderStatusesAsync(int branchId)
        {
            try
            {
                return await _http.GetJsonAsync<List<AccuratSystem.Contracts.Models.OrderStatuses>>($"OrderStatuses/by-branch/{branchId}")
                       ?? new List<AccuratSystem.Contracts.Models.OrderStatuses>();
            }
            catch (Exception)
            {
                return new List<AccuratSystem.Contracts.Models.OrderStatuses>();
            }
        }
        #endregion

        #region КЛИЕНТЫ (CLIENTS)
        public async Task<List<ContractsClient>> GetClientsAsync()
        {
            try { return await _http.GetJsonAsync<List<ContractsClient>>("Clients") ?? new List<ContractsClient>(); }
            catch (HttpRequestException ex) { throw new Exception($"Клиенты (Clients): {ex.Message}"); }
        }

        public async Task<ContractsClient> CreateClientAsync(ContractsClient client)
        {
            var response = await _http.PostJsonAsync("Clients", client);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadJsonAsync<ContractsClient>();
        }

        public async Task UpdateClientAsync(ContractsClient client)
        {
            var response = await _http.PutJsonAsync($"Clients/{client.Id}", client);
            response.EnsureSuccessStatusCode();
        }
        #endregion

        #region ЗАКАЗЫ (ORDERS)

        // Получение списка заказов с фильтрацией по дате
        public async Task<List<ContractsOrder>> GetOrdersAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                // Принудительно устанавливаем Kind = Utc для обеих дат
                DateTime start = startDate.HasValue
                    ? startDate.Value
                    : DateTime.UtcNow.AddDays(-1);

                DateTime end = endDate.HasValue
                    ? endDate.Value
                    : DateTime.UtcNow.AddDays(30);

                string url = $"Orders?startDate={Q(start)}&endDate={Q(end)}";

                return await _http.GetJsonAsync<List<ContractsOrder>>(url) ?? new List<ContractsOrder>();
            }
            catch (HttpRequestException ex) { throw new Exception("Ошибка при получении списка заказов: " + ex.Message); }
        }

        // Получение заказа по ID
        public async Task<ContractsOrder> GetOrderByIdAsync(int orderId)
        {
            try { return await _http.GetJsonAsync<ContractsOrder>($"Orders/{orderId}"); }
            catch { return null; }
        }

        // Получение заказов по ID клиента
        public async Task<List<ContractsOrder>> GetOrdersByClientIdAsync(int clientId)
        {
            try { return await _http.GetJsonAsync<List<ContractsOrder>>($"Orders/client/{clientId}") ?? new List<ContractsOrder>(); }
            catch (HttpRequestException ex) { throw new Exception($"История заказов клиента: {ex.Message}"); }
        }

        // Создание нового заказа
        public async Task<ContractsOrder> CreateOrderAsync(ContractsOrder order)
        {
            var response = await _http.PostJsonAsync("Orders", order);
            if (!response.IsSuccessStatusCode)
            {
                string errorText = await response.Content.ReadAsStringAsync();
                throw new Exception($"Отказ сервера ({response.StatusCode}): {errorText}");
            }
            return await response.Content.ReadJsonAsync<ContractsOrder>();
        }

        // Обновление существующего заказа
        public async Task UpdateOrderAsync(ContractsOrder order)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"Orders/{order.Id}");
            request.Content = JsonContent.Create(order, options: JsonOpts.Default);

            // ИСПРАВЛЕНО: передаём только ID (число, всегда ASCII)
            // Имя сервер сам достанет из БД по этому ID
            if (App.CurrentUser != null)
            {
                request.Headers.Add("X-User-Id", App.CurrentUser.Id.ToString());
            }

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                string errorText = await response.Content.ReadAsStringAsync();
                throw new Exception($"Отказ сервера ({response.StatusCode}): {errorText}");
            }
        }

        /// <summary>
        /// Получает журнал действий с фильтрами и пагинацией.
        /// </summary>
        public async Task<List<OrderTimelineEntry>> GetAuditLogAsync(
            int? userId = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string entryType = null,
            int pageSize = 100,
            int pageNumber = 1)
        {
            try
            {
                var queryParams = new List<string>();

                if (userId.HasValue)
                    queryParams.Add($"userId={userId.Value}");

                // ИСПРАВЛЕНО: принудительно конвертируем в UTC
                if (startDate.HasValue)
                {
                    var utcStart = startDate.Value;
                    queryParams.Add($"startDate={Q(utcStart)}");
                }

                if (endDate.HasValue)
                {
                    var utcEnd = endDate.Value;
                    queryParams.Add($"endDate={Q(utcEnd)}");
                }

                if (!string.IsNullOrWhiteSpace(entryType))
                    queryParams.Add($"entryType={Uri.EscapeDataString(entryType)}");

                queryParams.Add($"pageSize={pageSize}");
                queryParams.Add($"pageNumber={pageNumber}");

                var queryString = string.Join("&", queryParams);
                var url = $"Orders/audit-log?{queryString}";

                return await _http.GetJsonAsync<List<OrderTimelineEntry>>(url)
                       ?? new List<OrderTimelineEntry>();
            }
            catch (HttpRequestException ex)
            {
                // ИСПРАВЛЕНО: показываем детали ошибки
                throw new Exception($"Журнал аудита: {ex.Message}");
            }
        }

        // Получение активных заказов для конкретного филиала
        public async Task<List<ContractsOrder>> GetActiveOrdersAsync(int branchId)
        {
            try
            {
                var activeOrders = await _http.GetJsonAsync<List<ContractsOrder>>($"Orders/active/{branchId}");
                return activeOrders ?? new List<ContractsOrder>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении активных заказов: {ex.Message}");
                return new List<ContractsOrder>();
            }
        }

        // Завершение заказа (профессиональный переход)
        public async Task<bool> CompleteOrderAsync(int orderId)
        {
            try
            {
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"Orders/{orderId}/complete");
                var response = await _http.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при завершении заказа: {ex.Message}");
                return false;
            }
        }



        // Смена статуса заказа (профессиональный переход)
        public async Task<bool> ChangeStatusAsync(int orderId, string newStatus, int? userId, string userName)
        {
            try
            {
                var dto = new AccuratSystem.Contracts.DTOs.ChangeStatusDto
                {
                    NewStatus = newStatus,
                    UserId = userId,
                    UserName = userName
                };

                var response = await _http.PatchJsonAsync($"Orders/{orderId}/status", dto);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка смены статуса: {ex.Message}");
                return false;
            }
        }

        // Получение анализа времени (для графиков и отчетов)
        public async Task<List<dynamic>> GetTimeAnalysisAsync(int orderId)
        {
            try
            {
                return await _http.GetJsonAsync<List<dynamic>>($"Orders/{orderId}/time-analysis")
                       ?? new List<dynamic>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка анализа времени: {ex.Message}");
                return new List<dynamic>();
            }
        }

        // Удаление заказа (профессиональный переход)
        public async Task<bool> DeleteOrderAsync(int orderId)
        {
            try
            {
                var response = await _http.DeleteAsync($"Orders/{orderId}");
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка удаления заказа: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region ПРОВЕРКА ДОСТУПНОСТИ БОКСА
        public async Task<bool> CheckBoxAvailabilityForAppointmentAsync(int branchId, int box, DateTime startTime, int durationMinutes, int excludeOrderId = 0)
        {
            try
            {
                var url = $"Orders/check-availability?branchId={branchId}&box={box}&start={Q(startTime)}&duration={durationMinutes}&excludeOrderId={excludeOrderId}";
                var isAvailable = await _http.GetJsonAsync<bool>(url);
                return isAvailable;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка проверки доступности: {ex.Message}");
                return true;
            }
        }
        #endregion

        #region АВТОРИЗАЦИЯ И ФИЛИАЛЫ
        public async Task<List<ContractsBranch>> GetBranchesAsync()
        {
            try
            {
                var branches = await _http.GetJsonAsync<List<ContractsBranch>>("Branches");
                return branches ?? new List<ContractsBranch>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Скрытая ошибка API: {ex.Message}");
            }
        }
        public async Task<LoginResponseDto> AuthenticateAsync(string login, string password)
        {
            var request = new LoginRequestDto { Login = login, Password = password };
            var response = await _http.PostJsonAsync("Users/login", request);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadJsonAsync<LoginResponseDto>();

            return null;
        }

        public void UpdateTenantContext(int companyId)
        {
            // Этот метод теперь будет применять заголовок глобально ко всем окнам!
            if (_http.DefaultRequestHeaders.Contains("X-Company-Id"))
            {
                _http.DefaultRequestHeaders.Remove("X-Company-Id");
            }
            _http.DefaultRequestHeaders.Add("X-Company-Id", companyId.ToString());
        }

        public async Task UpdateBranchAsync(ContractsBranch branch)
        {
            var response = await _http.PutJsonAsync($"Branches/{branch.Id}", branch);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteBranchAsync(int id)
        {
            var response = await _http.DeleteAsync($"Branches/{id}");
            response.EnsureSuccessStatusCode();
        }

        public async Task<ContractsBranch> CreateBranchAsync(ContractsBranch branch)
        {
            var response = await _http.PostJsonAsync("Branches", branch);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadJsonAsync<ContractsBranch>();
        }
        #endregion

        #region СОТРУДНИКИ (USERS) (добавь к существующим)
        public async Task DeleteUserAsync(int id)
        {
            var response = await _http.DeleteAsync($"Users/{id}");
            response.EnsureSuccessStatusCode();
        }
        #endregion

        #region КЛИЕНТЫ (CLIENTS) (добавь к существующим)
        public async Task DeleteClientAsync(int id)
        {
            var response = await _http.DeleteAsync($"Clients/{id}");
            response.EnsureSuccessStatusCode();
        }

        #endregion

        #region ФИНАНСЫ (TRANSACTIONS)
        public async Task<List<ContractsTransaction>> GetTransactionsByBranchAsync(int branchId)
        {
            try { return await _http.GetJsonAsync<List<ContractsTransaction>>($"Transactions/branch/{branchId}") ?? new List<ContractsTransaction>(); }
            catch (HttpRequestException ex) { throw new Exception($"Финансы (Transactions): {ex.Message}"); }
        }

        public async Task<ContractsTransaction> CreateTransactionAsync(ContractsTransaction transaction)
        {
            var response = await _http.PostJsonAsync("Transactions", transaction);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadJsonAsync<ContractsTransaction>();
        }

        public async Task<ContractsCashboxSummary> GetShiftCashboxSummaryAsync(int shiftId)
        {
            try { return await _http.GetJsonAsync<ContractsCashboxSummary>($"Shifts/{shiftId}/cashbox") ?? new ContractsCashboxSummary(); }
            catch { return new ContractsCashboxSummary(); }
        }

        public async Task<List<ContractsTransaction>> GetTransactionsByShiftAsync(int shiftId)
        {
            try { return await _http.GetJsonAsync<List<ContractsTransaction>>($"Transactions/shift/{shiftId}") ?? new List<ContractsTransaction>(); }
            catch { return new List<ContractsTransaction>(); }
        }

        public async Task<AccuratSystem.Contracts.Models.CompanySettings> GetCompanySettingsAsync(int branchId)
        {
            try
            {
                return await _http.GetJsonAsync<AccuratSystem.Contracts.Models.CompanySettings>($"CompanySettings/by-branch/{branchId}")
                       ?? new AccuratSystem.Contracts.Models.CompanySettings();
            }
            catch (Exception)
            {
                return new AccuratSystem.Contracts.Models.CompanySettings();
            }
        }

        public async Task<OrderCalculation> CalculateOrderPreviewAsync(OrderPreviewRequestDto request)
        {
            try
            {
                var response = await _http.PostJsonAsync("orders/calculate-preview", request);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadJsonAsync<OrderCalculation>() ?? new OrderCalculation();
                }
                return new OrderCalculation();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка калькулятора: {ex.Message}");
                return new OrderCalculation();
            }
        }
        #endregion

        #region ОТЧЕТЫ (REPORTS)

        // Получение отчетов по сменам с фильтрацией по филиалу и диапазону дат
        public async Task<List<ContractsShiftReport>> GetShiftReportsAsync(int branchId, DateTime start, DateTime end)
        {
            try { return await _http.GetJsonAsync<List<ContractsShiftReport>>($"Reports/shifts?branchId={branchId}&start={Q(start)}&end={Q(end)}") ?? new List<ContractsShiftReport>(); }
            catch { return new List<ContractsShiftReport>(); }
        }

        // Получение статистики по клиентам с фильтрацией по филиалу и диапазону дат
        public async Task<ClientStatsResponse> GetClientsStatsAsync(int branchId, DateTime start, DateTime end)
        {
            try { return await _http.GetJsonAsync<ClientStatsResponse>($"Reports/clients-stats?branchId={branchId}&start={Q(start)}&end={Q(end)}") ?? new ClientStatsResponse(); }
            catch { return new ClientStatsResponse(); }
        }

        // Получение транзакций по филиалу и диапазону дат
        public async Task<List<ContractsTransaction>> GetTransactionsByDateRangeAsync(int branchId, DateTime start, DateTime end)
        {
            try { return await _http.GetJsonAsync<List<ContractsTransaction>>($"Transactions/range?branchId={branchId}&start={Q(start)}&end={Q(end)}") ?? new List<ContractsTransaction>(); }
            catch { return new List<ContractsTransaction>(); }
        }

        // Получение сводки по сверкам (Reconciliation Summary) с фильтрацией по филиалу и диапазону дат
        public class ReconciliationSummaryResponse
        {
            public int TotalShifts { get; set; }
            public int ReconciledShifts { get; set; }
            public decimal TotalDifference { get; set; }
        }

        // Метод для получения сводки по сверкам
        public async Task<ReconciliationSummaryResponse> GetReconciliationsSummaryAsync(int branchId, DateTime start, DateTime end)
        {
            try
            {
                return await _http.GetJsonAsync<ReconciliationSummaryResponse>($"Reports/reconciliations-summary?branchId={branchId}&start={Q(start)}&end={Q(end)}")
                       ?? new ReconciliationSummaryResponse();
            }
            catch { return new ReconciliationSummaryResponse(); }
        }

        /// <summary>
        /// Получает полное сравнение двух периодов
        /// </summary>
        public async Task<PeriodComparisonFull> GetPeriodComparisonFullAsync(
            int branchId,
            DateTime currentStart, DateTime currentEnd,
            DateTime previousStart, DateTime previousEnd)
        {
            var url = $"Reports/compare-periods-full" +
                $"?branchId={branchId}" +
                $"&currentStart={Q(currentStart)}&currentEnd={Q(currentEnd)}" +
                $"&previousStart={Q(previousStart)}&previousEnd={Q(previousEnd)}";

            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return Newtonsoft.Json.JsonConvert.DeserializeObject<PeriodComparisonFull>(json);
        }

        #endregion

        #region ГРАФИКИ (SCHEDULES) И КОНВЕРТАЦИЯ
        public async Task<List<ContractsEmployeeSchedule>> GetScheduleAsync(int branchId, int year, int month)
        {
            try
            {
                // Добавляем branchId в URL
                return await _http.GetJsonAsync<List<ContractsEmployeeSchedule>>($"Schedules/{branchId}/{year}/{month}")
                       ?? new List<ContractsEmployeeSchedule>();
            }
            catch { return new List<ContractsEmployeeSchedule>(); }
        }

        public async Task SaveScheduleAsync(int branchId, int year, int month, List<ContractsEmployeeSchedule> scheduleData)
        {
            // Добавляем branchId в URL
            var response = await _http.PostJsonAsync($"Schedules/{branchId}/{year}/{month}", scheduleData);
            response.EnsureSuccessStatusCode();
        }
        #endregion

        #region КОНВЕРТАЦИЯ ЗАПИСИ В ЗАКАЗ
        public async Task<ContractsOrder> ConvertAppointmentToOrderAsync(int appointmentId, int shiftId, int washerId)
        {
            // Бьем ровно по тому маршруту, который создали в контроллере
            var response = await _http.PostAsync($"Orders/{appointmentId}/convert?shiftId={shiftId}&washerId={washerId}", null);

            if (!response.IsSuccessStatusCode)
            {
                string errorText = await response.Content.ReadAsStringAsync();
                throw new Exception($"Отказ сервера ({response.StatusCode}): {errorText}");
            }

            return await response.Content.ReadJsonAsync<ContractsOrder>();
        }
        #endregion

        #region КАТЕГОРИИ АВТО

        public async Task<List<AccuratSystem.Contracts.Models.CarCategory>> GetCarCategoriesAsync(int branchId)
        {
            try
            {
                return await _http.GetJsonAsync<List<AccuratSystem.Contracts.Models.CarCategory>>($"CarCategories/by-branch/{branchId}")
                       ?? new List<AccuratSystem.Contracts.Models.CarCategory>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки категорий авто: {ex.Message}");
                return new List<AccuratSystem.Contracts.Models.CarCategory>();
            }
        }

        public async Task CreateCategoryAsync(AccuratSystem.Contracts.Models.CarCategory category)
        {
            var response = await _http.PostJsonAsync("CarCategories", category);
            response.EnsureSuccessStatusCode();
        }

        public async Task UpdateCategoryAsync(AccuratSystem.Contracts.Models.CarCategory category)
        {
            var response = await _http.PutJsonAsync($"CarCategories/{category.Id}", category);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteCategoryAsync(int id)
        {
            var response = await _http.DeleteAsync($"CarCategories/{id}");
            response.EnsureSuccessStatusCode();
        }

        #endregion

        #region РАСХОДЫ И ЛЕНТА (СЕРВИС)

        // Добавить расход к заказу
        public async Task<OrderExpense> AddOrderExpenseAsync(int orderId, AddOrderExpenseDto dto)
        {
            try
            {
                var response = await _http.PostJsonAsync($"Orders/{orderId}/expenses", dto);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadJsonAsync<OrderExpense>();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Ошибка добавления расхода: {ex.Message}");
            }
        }

        // Получить расходы по заказу
        public async Task<List<OrderExpense>> GetOrderExpensesAsync(int orderId)
        {
            try
            {
                return await _http.GetJsonAsync<List<OrderExpense>>($"Orders/{orderId}/expenses")
                    ?? new List<OrderExpense>();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Ошибка получения расходов: {ex.Message}");
            }
        }

        // Получить ленту событий заказа
        public async Task<List<OrderTimelineEntry>> GetOrderTimelineAsync(int orderId)
        {
            try
            {
                return await _http.GetJsonAsync<List<OrderTimelineEntry>>($"Orders/{orderId}/timeline")
                    ?? new List<OrderTimelineEntry>();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Ошибка получения ленты: {ex.Message}");
            }
        }

        // Обновить цену услуги в заказе
        public async Task<OrderServiceItem> UpdateServicePriceAsync(int orderServiceItemId, UpdateServicePriceDto dto)
        {
            try
            {
                var response = await _http.PutJsonAsync($"Orders/services/{orderServiceItemId}/price", dto);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadJsonAsync<OrderServiceItem>();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Ошибка обновления цены: {ex.Message}");
            }
        }

        #endregion

        #region УМНЫЙ КАССИР (UPSELL DLC)

        // Получить все правила апселла для текущей компании
        public async Task<List<UpsellSuggestion>> GetUpsellRulesAsync()
        {
            try { return await _http.GetJsonAsync<List<UpsellSuggestion>>("Upsell") ?? new List<UpsellSuggestion>(); }
            catch { return new List<UpsellSuggestion>(); }
        }

        // Создать новое правило апселла
        public async Task<UpsellSuggestion> CreateUpsellRuleAsync(UpsellSuggestion rule)
        {
            var response = await _http.PostJsonAsync("Upsell", rule);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadJsonAsync<UpsellSuggestion>();
        }

        // Удалить существующее правило апселла
        public async Task DeleteUpsellRuleAsync(int id)
        {
            var response = await _http.DeleteAsync($"Upsell/{id}");
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Получить подсказку апселла для текущего набора услуг.
        /// Возвращает null, если правила нет (это нормально, не ошибка).
        /// Внутренне использует защищённый GetFromJsonAsync, который корректно
        /// обрабатывает пустое тело и 404 — не падает с JsonException.
        /// </summary>
        public async Task<UpsellSuggestion?> GetUpsellSuggestionAsync(List<int> selectedServiceIds, int branchId)
        {
            if (selectedServiceIds == null || !selectedServiceIds.Any() || branchId <= 0)
                return null;

            var query = string.Join("&", selectedServiceIds.Select(id => $"currentServices={id}"));
            var url = $"Upsell/suggest?{query}&branchId={branchId}";

            return await SafeGetAsync<UpsellSuggestion>(url);
        }

        #endregion

        #region СУПЕРАДМИН (COMPANIES)
        public async Task<List<Company>> GetCompaniesAsync()
        {
            try { return await _http.GetJsonAsync<List<Company>>("Companies") ?? new List<Company>(); }
            catch { return new List<Company>(); }
        }

        public async Task<Company> CreateCompanyAsync(Company company)
        {
            var response = await _http.PostJsonAsync("Companies", company);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadJsonAsync<Company>();
        }

        public async Task UpdateCompanyAsync(Company company)
        {
            var response = await _http.PutJsonAsync($"Companies/{company.Id}", company);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteCompanyAsync(int id)
        {
            var response = await _http.DeleteAsync($"Companies/{id}");
            response.EnsureSuccessStatusCode();
        }
        #endregion

        #region УПРАВЛЕНИЕ ЛИЦЕНЗИЯМИ (TENANT FEATURES)
        public async Task<List<TenantFeature>> GetTenantFeaturesAsync()
        {
            try { return await _http.GetJsonAsync<List<TenantFeature>>("TenantFeatures") ?? new List<TenantFeature>(); }
            catch { return new List<TenantFeature>(); }
        }

        public async Task UpdateTenantFeatureAsync(TenantFeature feature)
        {
            // Меняем feature.BranchId на feature.CompanyId
            var response = await _http.PutJsonAsync($"TenantFeatures/{feature.CompanyId}", feature);
            response.EnsureSuccessStatusCode();
        }

        // Опционально добавь метод удаления
        public async Task DeleteTenantFeatureAsync(int id)
        {
            var response = await _http.DeleteAsync($"TenantFeatures/{id}");
            response.EnsureSuccessStatusCode();
        }
        #endregion

        #region СКИДКИ (DISCOUNT RULES)
        public async Task<List<DiscountRule>> GetDiscountRulesAsync()
        {
            try
            {
                return await _http.GetJsonAsync<List<DiscountRule>>("DiscountRules") ?? new List<DiscountRule>();
            }
            catch (Exception ex) { throw new Exception($"Ошибка загрузки правил скидок: {ex.Message}"); }
        }

        public async Task CreateDiscountRuleAsync(DiscountRule rule)
        {
            var response = await _http.PostJsonAsync("DiscountRules", rule);
            response.EnsureSuccessStatusCode();
        }

        public async Task UpdateDiscountRuleAsync(DiscountRule rule)
        {
            var response = await _http.PutJsonAsync($"DiscountRules/{rule.Id}", rule);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteDiscountRuleAsync(int id)
        {
            var response = await _http.DeleteAsync($"DiscountRules/{id}");
            response.EnsureSuccessStatusCode();
        }
        #endregion

        #region РОЛИ (ROLES)

        // К Ролям (Roles) добавь методы Create, Update, Delete (GET у тебя уже есть)
        // Создание новой роли
        public async Task<Role> CreateRoleAsync(Role role)
        {
            var response = await _http.PostJsonAsync("Roles", role);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadJsonAsync<Role>();
        }

        // Обновление существующей роли
        public async Task UpdateRoleAsync(Role role)
        {
            var response = await _http.PutJsonAsync($"Roles/{role.Id}", role);
            response.EnsureSuccessStatusCode();
        }

        // Удаление роли
        public async Task DeleteRoleAsync(int id)
        {
            var response = await _http.DeleteAsync($"Roles/{id}");
            response.EnsureSuccessStatusCode();
        }

        // DTO для статистики клиентов (Client Stats)
        public class ClientStatsResponse
        {
            public int NewClients { get; set; } // количество новых клиентов за период
            public int UniqueClients { get; set; } // количество уникальных клиентов за период
            public int RepeatClients { get; set; }  // количество повторных клиентов за период
            public decimal RetentionRate { get; set; }  // коэффициент удержания клиентов за период
        }

        /// <summary>
        /// Безопасная версия GET: ловит 404/пустое тело/"null" и возвращает default.
        /// Сериализация через JsonOpts.Default (с конвертером).
        /// </summary>
        private async Task<T?> SafeGetAsync<T>(string url)
        {
            try
            {
                var response = await _http.GetAsync(url);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return default;

                // Логируем тело ответа и URL при ошибочном коде, чтобы понять причину 400/5xx
                if (!response.IsSuccessStatusCode)
                {
                    var requestUri = response.RequestMessage?.RequestUri?.ToString() ?? url;
                    var errorBody = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"HTTP GET {requestUri} -> {(int)response.StatusCode} {response.ReasonPhrase}. Body: {errorBody}");
                    return default;
                }   

                var content = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(content) || content == "null")
                    return default;

                return System.Text.Json.JsonSerializer.Deserialize<T>(content, JsonOpts.Default);
            }
            catch (HttpRequestException)
            {
                return default;
            }
        }

        #endregion

        #region X-ОТЧЕТ (СВЕРКА КАССЫ)
        //Проведение сверки кассы(X - отчёт) по смене 
        public async Task<ReconcileCashResult> ReconcileCashAsync(int shiftId, ReconcileCashRequest request)
        {
            var response = await _http.PostJsonAsync($"Shifts/{shiftId}/reconcile", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadJsonAsync<ReconcileCashResult>();
        }

        // Получение списка сверок по смене
        public async Task<List<CashReconciliation>> GetReconciliationsAsync(int shiftId)
        {
            // Используем хелпер: он ловит 404 как "нет данных" и возвращает default (null).
            // Прямой _http.GetFromJsonAsync выбрасывал HttpRequestException.
            var result = await SafeGetAsync<List<CashReconciliation>>($"Shifts/{shiftId}/reconciliations");
            return result ?? new List<CashReconciliation>();
        }

        // Получение ленты событий смены (Shift Timeline)
        public async Task<List<ShiftTimelineEntry>> GetShiftTimelineAsync(int shiftId)
        {
            var result = await SafeGetAsync<List<ShiftTimelineEntry>>($"Shifts/{shiftId}/timeline");
            return result ?? new List<ShiftTimelineEntry>();
        }
        #endregion

        #region ЕДИНЫЙ ЖУРНАЛ ДЕЙСТВИЙ (AUDIT LOG)
        public async Task<List<AuditEntryDto>> GetAuditLogAsync(
            int? userId = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string entryType = null,
            int? branchId = null,
            int pageSize = 500,
            int pageNumber = 1)
        {
            var queryParams = new List<string>();

            if (userId.HasValue)
                queryParams.Add($"userId={userId.Value}");

            if (startDate.HasValue)
                queryParams.Add($"startDate={Q(startDate.Value)}");

            if (endDate.HasValue)
                queryParams.Add($"endDate={Q(endDate.Value)}");

            if (!string.IsNullOrWhiteSpace(entryType))
                queryParams.Add($"entryType={Uri.EscapeDataString(entryType)}");

            if (branchId.HasValue)
                queryParams.Add($"branchId={branchId.Value}");

            queryParams.Add($"pageSize={pageSize}");
            queryParams.Add($"pageNumber={pageNumber}");

            var url = $"audit-log?{string.Join("&", queryParams)}";

            // Хелпер GetJsonAsync<T> ловит 404 и возвращает default (null).
            // Прямой _http.GetFromJsonAsync выбрасывал HttpRequestException.
            var result = await SafeGetAsync<List<AuditEntryDto>>(url);
            return result ?? new List<AuditEntryDto>();
        }
        #endregion
    }
}