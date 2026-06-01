// === ЯВНЫЕ АЛИАСЫ ДЛЯ КОНТРАКТНЫХ МОДЕЛЕЙ (чтобы избежать конфликтов с UI-моделями) ===
// === UI-МОДЕЛИ (без алиасов, так как они в том же неймспейсе) ===
using AccuratPanelCarWashing.Models;
using AccuratPanelCarWashing.Services;
using AccuratSystem.Contracts.DTOs;
using AccuratSystem.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AccuratSystem.Contracts.DTOs;
using System.Net.Http.Json;
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

namespace AccuratPanelCarWashing.Services
{
    public class ApiService
    {
        private static readonly HttpClient _http;

        static ApiService()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true;

            _http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7165/api/") };
        }

        public ApiService()
        {
            // Обычный конструктор теперь пустой
        }

        #region СМЕНЫ (SHIFTS)
        public async Task<List<ContractsShift>> GetShiftsAsync()
        {
            try { return await _http.GetFromJsonAsync<List<ContractsShift>>("Shifts") ?? new List<ContractsShift>(); }
            catch (HttpRequestException ex) { throw new Exception($"Смены (Shifts): {ex.Message}"); }
        }

        public async Task<ContractsShift> OpenShiftAsync(ContractsShift shift)
        {
            var response = await _http.PostAsJsonAsync("Shifts", shift);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ContractsShift>();
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
            try { return await _http.GetFromJsonAsync<List<ContractsService>>("Services") ?? new List<ContractsService>(); }
            catch (HttpRequestException ex) { throw new Exception($"Услуги (Services): {ex.Message}"); }
        }

        public async Task<ContractsService> CreateServiceAsync(ContractsService service)
        {
            var response = await _http.PostAsJsonAsync("Services", service);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ContractsService>();
        }

        public async Task UpdateServiceAsync(ContractsService service)
        {
            var response = await _http.PutAsJsonAsync($"Services/{service.Id}", service);
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
            try { return await _http.GetFromJsonAsync<List<ContractsUser>>("Users") ?? new List<ContractsUser>(); }
            catch (HttpRequestException ex) { throw new Exception($"Сотрудники (Users): {ex.Message}"); }
        }

        public async Task<ContractsUser> CreateUserAsync(ContractsUser user)
        {
            var response = await _http.PostAsJsonAsync("Users", user);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ContractsUser>();
        }

        public async Task UpdateUserAsync(ContractsUser user)
        {
            var response = await _http.PutAsJsonAsync($"Users/{user.Id}", user);
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<Role>> GetRolesAsync()
        {
            try { return await _http.GetFromJsonAsync<List<Role>>("Roles") ?? new List<Role>(); }
            catch (HttpRequestException ex) { throw new Exception($"Ошибка получения должностей: {ex.Message}"); }
        }

        public async Task<List<AccuratSystem.Contracts.Models.CarCategory>> GetCarCategoriesAsync(int branchId)
        {
            try
            {
                return await _http.GetFromJsonAsync<List<AccuratSystem.Contracts.Models.CarCategory>>($"CarCategories/by-branch/{branchId}")
                       ?? new List<AccuratSystem.Contracts.Models.CarCategory>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки категорий авто: {ex.Message}");
                return new List<AccuratSystem.Contracts.Models.CarCategory>();
            }
        }

        public async Task<List<AccuratSystem.Contracts.Models.PaymentMethod>> GetPaymentMethodsAsync(int branchId)
        {
            try
            {
                return await _http.GetFromJsonAsync<List<AccuratSystem.Contracts.Models.PaymentMethod>>($"PaymentMethods/by-branch/{branchId}")
                       ?? new List<AccuratSystem.Contracts.Models.PaymentMethod>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки способов оплаты: {ex.Message}");
                return new List<AccuratSystem.Contracts.Models.PaymentMethod>();
            }
        }

        public async Task<List<AccuratSystem.Contracts.Models.OrderStatus>> GetOrderStatusesAsync(int branchId)
        {
            try
            {
                return await _http.GetFromJsonAsync<List<AccuratSystem.Contracts.Models.OrderStatus>>($"OrderStatuses/by-branch/{branchId}")
                       ?? new List<AccuratSystem.Contracts.Models.OrderStatus>();
            }
            catch (Exception)
            {
                return new List<AccuratSystem.Contracts.Models.OrderStatus>();
            }
        }
        #endregion

        #region КЛИЕНТЫ (CLIENTS)
        public async Task<List<ContractsClient>> GetClientsAsync()
        {
            try { return await _http.GetFromJsonAsync<List<ContractsClient>>("Clients") ?? new List<ContractsClient>(); }
            catch (HttpRequestException ex) { throw new Exception($"Клиенты (Clients): {ex.Message}"); }
        }

        public async Task<ContractsClient> CreateClientAsync(ContractsClient client)
        {
            var response = await _http.PostAsJsonAsync("Clients", client);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ContractsClient>();
        }

        public async Task UpdateClientAsync(ContractsClient client)
        {
            var response = await _http.PutAsJsonAsync($"Clients/{client.Id}", client);
            response.EnsureSuccessStatusCode();
        }
        #endregion

        #region ЗАКАЗЫ (ORDERS)
        public async Task<List<ContractsOrder>> GetOrdersAsync()
        {
            try { return await _http.GetFromJsonAsync<List<ContractsOrder>>("Orders") ?? new List<ContractsOrder>(); }
            catch (HttpRequestException ex) { throw new Exception("Ошибка при получении списка заказов: " + ex.Message); }
        }

        public async Task<List<ContractsOrder>> GetOrdersByClientIdAsync(int clientId)
        {
            try { return await _http.GetFromJsonAsync<List<ContractsOrder>>($"Orders/client/{clientId}") ?? new List<ContractsOrder>(); }
            catch (HttpRequestException ex) { throw new Exception($"История заказов клиента: {ex.Message}"); }
        }

        public async Task<ContractsOrder> CreateOrderAsync(ContractsOrder order)
        {
            var response = await _http.PostAsJsonAsync("Orders", order);
            if (!response.IsSuccessStatusCode)
            {
                string errorText = await response.Content.ReadAsStringAsync();
                throw new Exception($"Отказ сервера ({response.StatusCode}): {errorText}");
            }
            return await response.Content.ReadFromJsonAsync<ContractsOrder>();
        }

        public async Task UpdateOrderAsync(ContractsOrder order)
        {
            var response = await _http.PutAsJsonAsync($"Orders/{order.Id}", order);
            if (!response.IsSuccessStatusCode)
            {
                string errorText = await response.Content.ReadAsStringAsync();
                throw new Exception($"Отказ сервера ({response.StatusCode}): {errorText}");
            }
        }

        public async Task<List<ContractsOrder>> GetActiveOrdersAsync(int branchId)
        {
            try
            {
                var activeOrders = await _http.GetFromJsonAsync<List<ContractsOrder>>($"Orders/active/{branchId}");
                return activeOrders ?? new List<ContractsOrder>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении активных заказов: {ex.Message}");
                return new List<ContractsOrder>();
            }
        }

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



        // 1. Смена статуса заказа (профессиональный переход)
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

                var response = await _http.PatchAsJsonAsync($"Orders/{orderId}/status", dto);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка смены статуса: {ex.Message}");
                return false;
            }
        }

        // 2. Получение анализа времени (для графиков и отчетов)
        public async Task<List<dynamic>> GetTimeAnalysisAsync(int orderId)
        {
            try
            {
                return await _http.GetFromJsonAsync<List<dynamic>>($"Orders/{orderId}/time-analysis")
                       ?? new List<dynamic>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка анализа времени: {ex.Message}");
                return new List<dynamic>();
            }
        }

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
                var url = $"Orders/check-availability?branchId={branchId}&box={box}&start={startTime:O}&duration={durationMinutes}&excludeOrderId={excludeOrderId}";
                var isAvailable = await _http.GetFromJsonAsync<bool>(url);
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
                var branches = await _http.GetFromJsonAsync<List<ContractsBranch>>("Branches");
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
            var response = await _http.PostAsJsonAsync("Users/login", request);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<LoginResponseDto>();

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
            var response = await _http.PutAsJsonAsync($"Branches/{branch.Id}", branch);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteBranchAsync(int id)
        {
            var response = await _http.DeleteAsync($"Branches/{id}");
            response.EnsureSuccessStatusCode();
        }

        public async Task<ContractsBranch> CreateBranchAsync(ContractsBranch branch)
        {
            var response = await _http.PostAsJsonAsync("Branches", branch);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ContractsBranch>();
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
            try { return await _http.GetFromJsonAsync<List<ContractsTransaction>>($"Transactions/branch/{branchId}") ?? new List<ContractsTransaction>(); }
            catch (HttpRequestException ex) { throw new Exception($"Финансы (Transactions): {ex.Message}"); }
        }

        public async Task<ContractsTransaction> CreateTransactionAsync(ContractsTransaction transaction)
        {
            var response = await _http.PostAsJsonAsync("Transactions", transaction);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ContractsTransaction>();
        }

        public async Task<ContractsCashboxSummary> GetShiftCashboxSummaryAsync(int shiftId)
        {
            try { return await _http.GetFromJsonAsync<ContractsCashboxSummary>($"Shifts/{shiftId}/cashbox") ?? new ContractsCashboxSummary(); }
            catch { return new ContractsCashboxSummary(); }
        }

        public async Task<List<ContractsTransaction>> GetTransactionsByShiftAsync(int shiftId)
        {
            try { return await _http.GetFromJsonAsync<List<ContractsTransaction>>($"Transactions/shift/{shiftId}") ?? new List<ContractsTransaction>(); }
            catch { return new List<ContractsTransaction>(); }
        }

        public async Task<AccuratSystem.Contracts.Models.CompanySettings> GetCompanySettingsAsync(int branchId)
        {
            try
            {
                return await _http.GetFromJsonAsync<AccuratSystem.Contracts.Models.CompanySettings>($"CompanySettings/by-branch/{branchId}")
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
                var response = await _http.PostAsJsonAsync("orders/calculate-preview", request);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<OrderCalculation>() ?? new OrderCalculation();
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
        public async Task<List<ContractsShiftReport>> GetShiftReportsAsync(int branchId, DateTime start, DateTime end)
        {
            try { return await _http.GetFromJsonAsync<List<ContractsShiftReport>>($"Reports/shifts?branchId={branchId}&start={start:O}&end={end:O}") ?? new List<ContractsShiftReport>(); }
            catch { return new List<ContractsShiftReport>(); }
        }

        public async Task<ClientStatsResponse> GetClientsStatsAsync(int branchId, DateTime start, DateTime end)
        {
            try { return await _http.GetFromJsonAsync<ClientStatsResponse>($"Reports/clients-stats?branchId={branchId}&start={start:O}&end={end:O}") ?? new ClientStatsResponse(); }
            catch { return new ClientStatsResponse(); }
        }

        public async Task<List<ContractsTransaction>> GetTransactionsByDateRangeAsync(int branchId, DateTime start, DateTime end)
        {
            try { return await _http.GetFromJsonAsync<List<ContractsTransaction>>($"Transactions/range?branchId={branchId}&start={start:O}&end={end:O}") ?? new List<ContractsTransaction>(); }
            catch { return new List<ContractsTransaction>(); }
        }
        #endregion

        #region ГРАФИКИ (SCHEDULES) И КОНВЕРТАЦИЯ
        public async Task<List<ContractsEmployeeSchedule>> GetScheduleAsync(int branchId, int year, int month)
        {
            try
            {
                // Добавляем branchId в URL
                return await _http.GetFromJsonAsync<List<ContractsEmployeeSchedule>>($"Schedules/{branchId}/{year}/{month}")
                       ?? new List<ContractsEmployeeSchedule>();
            }
            catch { return new List<ContractsEmployeeSchedule>(); }
        }

        public async Task SaveScheduleAsync(int branchId, int year, int month, List<ContractsEmployeeSchedule> scheduleData)
        {
            // Добавляем branchId в URL
            var response = await _http.PostAsJsonAsync($"Schedules/{branchId}/{year}/{month}", scheduleData);
            response.EnsureSuccessStatusCode();
        }
        #endregion

        #region ConvertAppointmentToOrderAsync
        public async Task<ContractsOrder> ConvertAppointmentToOrderAsync(int appointmentId, int shiftId, int washerId)
        {
            // Бьем ровно по тому маршруту, который создали в контроллере
            var response = await _http.PostAsync($"Orders/{appointmentId}/convert?shiftId={shiftId}&washerId={washerId}", null);

            if (!response.IsSuccessStatusCode)
            {
                string errorText = await response.Content.ReadAsStringAsync();
                throw new Exception($"Отказ сервера ({response.StatusCode}): {errorText}");
            }

            return await response.Content.ReadFromJsonAsync<ContractsOrder>();
        }
        #endregion

        // === ДОБАВИТЬ В КОНЕЦ КЛАССА ===

        #region РАСХОДЫ И ЛЕНТА (СЕРВИС)

        // Добавить расход к заказу
        public async Task<OrderExpense> AddOrderExpenseAsync(int orderId, AddOrderExpenseDto dto)
        {
            try
            {
                var response = await _http.PostAsJsonAsync($"Orders/{orderId}/expenses", dto);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<OrderExpense>();
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
                return await _http.GetFromJsonAsync<List<OrderExpense>>($"Orders/{orderId}/expenses")
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
                return await _http.GetFromJsonAsync<List<OrderTimelineEntry>>($"Orders/{orderId}/timeline")
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
                var response = await _http.PutAsJsonAsync($"Orders/services/{orderServiceItemId}/price", dto);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<OrderServiceItem>();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Ошибка обновления цены: {ex.Message}");
            }
        }

        #endregion

        #region УМНЫЙ КАССИР (UPSELL DLC)

        public async Task<List<UpsellSuggestion>> GetUpsellRulesAsync()
        {
            try { return await _http.GetFromJsonAsync<List<UpsellSuggestion>>("Upsell") ?? new List<UpsellSuggestion>(); }
            catch { return new List<UpsellSuggestion>(); }
        }

        public async Task<UpsellSuggestion> CreateUpsellRuleAsync(UpsellSuggestion rule)
        {
            var response = await _http.PostAsJsonAsync("Upsell", rule);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<UpsellSuggestion>();
        }

        public async Task DeleteUpsellRuleAsync(int id)
        {
            var response = await _http.DeleteAsync($"Upsell/{id}");
            response.EnsureSuccessStatusCode();
        }

        #endregion

        #region СУПЕРАДМИН (COMPANIES)
        public async Task<List<Company>> GetCompaniesAsync()
        {
            try { return await _http.GetFromJsonAsync<List<Company>>("Companies") ?? new List<Company>(); }
            catch { return new List<Company>(); }
        }

        public async Task<Company> CreateCompanyAsync(Company company)
        {
            var response = await _http.PostAsJsonAsync("Companies", company);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Company>();
        }

        public async Task UpdateCompanyAsync(Company company)
        {
            var response = await _http.PutAsJsonAsync($"Companies/{company.Id}", company);
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
            try { return await _http.GetFromJsonAsync<List<TenantFeature>>("TenantFeatures") ?? new List<TenantFeature>(); }
            catch { return new List<TenantFeature>(); }
        }

        public async Task UpdateTenantFeatureAsync(TenantFeature feature)
        {
            // Меняем feature.BranchId на feature.CompanyId
            var response = await _http.PutAsJsonAsync($"TenantFeatures/{feature.CompanyId}", feature);
            response.EnsureSuccessStatusCode();
        }

        // Опционально добавь метод удаления
        public async Task DeleteTenantFeatureAsync(int id)
        {
            var response = await _http.DeleteAsync($"TenantFeatures/{id}");
            response.EnsureSuccessStatusCode();
        }
        #endregion

        // К Ролям (Roles) добавь методы Create, Update, Delete (GET у тебя уже есть)
        public async Task<Role> CreateRoleAsync(Role role)
        {
            var response = await _http.PostAsJsonAsync("Roles", role);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Role>();
        }

        public async Task UpdateRoleAsync(Role role)
        {
            var response = await _http.PutAsJsonAsync($"Roles/{role.Id}", role);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteRoleAsync(int id)
        {
            var response = await _http.DeleteAsync($"Roles/{id}");
            response.EnsureSuccessStatusCode();
        }

        public class ClientStatsResponse
        {
            public int NewClients { get; set; }
            public int UniqueClients { get; set; }
        }

        public async Task<T> GetFromJsonAsync<T>(string url)
        {
            try
            {
                return await _http.GetFromJsonAsync<T>(url);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"API Error: {ex.Message}");
                return default;
            }
        }
    }
}