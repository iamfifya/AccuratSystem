using AccuratSystem.Contracts.Models;
using AccuratSystem.Contracts.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace AccuratPanelCWM.Services
{
    public class ApiService
    {
        private readonly HttpClient _http;

        // DI-контейнер MAUI сам передает нам готовый и настроенный HttpClient
        public ApiService(IHttpClientFactory httpClientFactory)
        {
            _http = httpClientFactory.CreateClient("ApiClient");
        }

        public void UpdateBaseUrl(string newUrl)
        {
            if (!newUrl.EndsWith("/")) newUrl += "/";
            _http.BaseAddress = new Uri(newUrl);
            Microsoft.Maui.Storage.Preferences.Default.Set("ServerUrl", newUrl);
        }

        #region СМЕНЫ (SHIFTS)
        public async Task<List<Shift>> GetShiftsAsync()
        {
            try { return await _http.GetFromJsonAsync<List<Shift>>("Shifts") ?? new List<Shift>(); }
            catch (HttpRequestException ex) { throw new Exception($"Смены (Shifts): {ex.Message}"); }
        }

        public async Task<Shift> OpenShiftAsync(Shift shift)
        {
            var response = await _http.PostAsJsonAsync("Shifts", shift);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Shift>();
        }

        public async Task CloseShiftAsync(int id)
        {
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"Shifts/{id}/close");
            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        public async Task<Shift> GetCurrentOpenShiftAsync()
        {
            var shifts = await GetShiftsAsync();
            return shifts.FirstOrDefault(s => !s.IsClosed);
        }
        #endregion

        #region УСЛУГИ (SERVICES)
        public async Task<List<Service>> GetServicesAsync()
        {
            try { return await _http.GetFromJsonAsync<List<Service>>("Services") ?? new List<Service>(); }
            catch (HttpRequestException ex) { throw new Exception($"Услуги (Services): {ex.Message}"); }
        }

        public async Task<Service> CreateServiceAsync(Service service)
        {
            var response = await _http.PostAsJsonAsync("Services", service);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Service>();
        }

        public async Task UpdateServiceAsync(Service service)
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
        public async Task<List<User>> GetUsersAsync()
        {
            try { return await _http.GetFromJsonAsync<List<User>>("Users") ?? new List<User>(); }
            catch (HttpRequestException ex) { throw new Exception($"Сотрудники (Users): {ex.Message}"); }
        }

        public async Task<User> CreateUserAsync(User user)
        {
            var response = await _http.PostAsJsonAsync("Users", user);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<User>();
        }

        public async Task UpdateUserAsync(User user)
        {
            var response = await _http.PutAsJsonAsync($"Users/{user.Id}", user);
            response.EnsureSuccessStatusCode();
        }
        #endregion

        #region КЛИЕНТЫ (CLIENTS)
        public async Task<List<Client>> GetClientsAsync()
        {
            try { return await _http.GetFromJsonAsync<List<Client>>("Clients") ?? new List<Client>(); }
            catch (HttpRequestException ex) { throw new Exception($"Клиенты (Clients): {ex.Message}"); }
        }

        public async Task<Client> CreateClientAsync(Client client)
        {
            var response = await _http.PostAsJsonAsync("Clients", client);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Client>();
        }

        public async Task UpdateClientAsync(Client client)
        {
            var response = await _http.PutAsJsonAsync($"Clients/{client.Id}", client);
            response.EnsureSuccessStatusCode();
        }
        #endregion

        #region ЗАКАЗЫ (ORDERS)
        public async Task<List<Order>> GetOrdersAsync() // Изменил CarWashOrder на Order
        {
            try { return await _http.GetFromJsonAsync<List<Order>>("Orders") ?? new List<Order>(); }
            catch (HttpRequestException ex) { throw new Exception("Ошибка при получении списка заказов: " + ex.Message); }
        }

        public async Task<List<Order>> GetOrdersByClientIdAsync(int clientId)
        {
            try { return await _http.GetFromJsonAsync<List<Order>>($"Orders/client/{clientId}") ?? new List<Order>(); }
            catch (HttpRequestException ex) { throw new Exception($"История заказов клиента: {ex.Message}"); }
        }

        public async Task<Order> CreateOrderAsync(Order order)
        {
            var response = await _http.PostAsJsonAsync("Orders", order);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Order>();
        }

        public async Task UpdateOrderAsync(Order order)
        {
            var response = await _http.PutAsJsonAsync($"Orders/{order.Id}", order);
            response.EnsureSuccessStatusCode();
        }

        public async Task<Client> GetClientByNumberAsync(string carNumber)
        {
            try
            {
                var response = await _http.GetAsync($"Clients/number/{carNumber}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<Client>();
                }
            }
            catch { }
            return null;
        }
        #endregion

        #region ПРОВЕРКА ДОСТУПНОСТИ БОКСА
        public async Task<bool> CheckBoxAvailabilityForAppointmentAsync(int branchId, int box, DateTime startTime, int durationMinutes, int excludeOrderId = 0)
        {
            try
            {
                var url = $"Orders/check-availability?branchId={branchId}&box={box}&start={startTime:O}&duration={durationMinutes}&excludeOrderId={excludeOrderId}";
                return await _http.GetFromJsonAsync<bool>(url);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка проверки доступности: {ex.Message}");
                return true;
            }
        }
        #endregion

        #region АВТОРИЗАЦИЯ И ФИЛИАЛЫ
        public async Task<List<Branch>> GetBranchesAsync()
        {
            try { return await _http.GetFromJsonAsync<List<Branch>>("Branches") ?? new List<Branch>(); }
            catch { return new List<Branch>(); }
        }

        public async Task<LoginResponseDto> AuthenticateAsync(string login, string password)
        {
            try
            {
                var request = new LoginRequestDto { Login = login, Password = password };
                var response = await _http.PostAsJsonAsync("Users/login", request);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<LoginResponseDto>();
                }
                else
                {
                    string errorText = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Ошибка сервера ({response.StatusCode}): {errorText}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Сбой подключения: {ex.Message}");
            }
        }
        #endregion

        #region ФИНАНСЫ (TRANSACTIONS)
        public async Task<List<Transaction>> GetTransactionsByBranchAsync(int branchId)
        {
            try { return await _http.GetFromJsonAsync<List<Transaction>>($"Transactions/branch/{branchId}") ?? new List<Transaction>(); }
            catch (HttpRequestException ex) { throw new Exception($"Финансы (Transactions): {ex.Message}"); }
        }

        public async Task<Transaction> CreateTransactionAsync(Transaction transaction)
        {
            var response = await _http.PostAsJsonAsync("Transactions", transaction);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Transaction>();
        }

        public class CashboxSummary { public decimal CashInHand { get; set; } public decimal TotalExpenses { get; set; } public decimal NetCashProfit { get; set; } }

        public async Task<CashboxSummary> GetShiftCashboxSummaryAsync(int shiftId)
        {
            try { return await _http.GetFromJsonAsync<CashboxSummary>($"Shifts/{shiftId}/cashbox") ?? new CashboxSummary(); }
            catch { return new CashboxSummary(); }
        }

        public async Task<List<Transaction>> GetTransactionsByShiftAsync(int shiftId)
        {
            try { return await _http.GetFromJsonAsync<List<Transaction>>($"Transactions/shift/{shiftId}") ?? new List<Transaction>(); }
            catch { return new List<Transaction>(); }
        }
        #endregion

        #region ОТЧЕТЫ (REPORTS)
        // ИСПОЛЬЗУЕМ КЛАССЫ ОТЧЕТОВ ИЗ КОНТРАКТОВ
        public async Task<List<ShiftReport>> GetShiftReportsAsync(int branchId, DateTime start, DateTime end)
        {
            try { return await _http.GetFromJsonAsync<List<ShiftReport>>($"Reports/shifts?branchId={branchId}&start={start:O}&end={end:O}") ?? new List<ShiftReport>(); }
            catch { return new List<ShiftReport>(); }
        }

        public class ClientStatsResponse { public int NewClients { get; set; } public int UniqueClients { get; set; } }

        public async Task<ClientStatsResponse> GetClientsStatsAsync(int branchId, DateTime start, DateTime end)
        {
            try { return await _http.GetFromJsonAsync<ClientStatsResponse>($"Reports/clients-stats?branchId={branchId}&start={start:O}&end={end:O}") ?? new ClientStatsResponse(); }
            catch { return new ClientStatsResponse(); }
        }

        public async Task<List<Transaction>> GetTransactionsByDateRangeAsync(int branchId, DateTime start, DateTime end)
        {
            try { return await _http.GetFromJsonAsync<List<Transaction>>($"Transactions/range?branchId={branchId}&start={start:O}&end={end:O}") ?? new List<Transaction>(); }
            catch { return new List<Transaction>(); }
        }
        #endregion

        #region ГРАФИКИ (SCHEDULES) И КОНВЕРТАЦИЯ
        public async Task<List<AccuratSystem.Contracts.Models.EmployeeSchedule>> GetScheduleAsync(int year, int month)
        {
            try { return await _http.GetFromJsonAsync<List<AccuratSystem.Contracts.Models.EmployeeSchedule>>($"Schedules/{year}/{month}") ?? new List<AccuratSystem.Contracts.Models.EmployeeSchedule>(); }
            catch { return new List<AccuratSystem.Contracts.Models.EmployeeSchedule>(); }
        }

        public async Task SaveScheduleAsync(int year, int month, List<AccuratSystem.Contracts.Models.EmployeeSchedule> scheduleData)
        {
            var response = await _http.PostAsJsonAsync($"Schedules/{year}/{month}", scheduleData);
            response.EnsureSuccessStatusCode();
        }

        public async Task<Order> ConvertAppointmentToOrderAsync(int appointmentId, int shiftId, int washerId)
        {
            var response = await _http.PostAsync($"Appointments/{appointmentId}/convert?shiftId={shiftId}&washerId={washerId}", null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Order>();
        }
        #endregion

        // 1. Получить активные заказы
        public async Task<List<Order>> GetActiveOrdersAsync(int branchId)
        {
            try
            {
                var activeOrders = await _http.GetFromJsonAsync<List<Order>>($"orders/active/{branchId}");
                return activeOrders ?? new List<Order>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении активных заказов: {ex.Message}");
                return new List<Order>();
            }
        }

        // 2. Завершить заказ
        public async Task<bool> CompleteOrderAsync(int orderId, string paymentMethod)
        {
            try
            {
                string url = $"Orders/{orderId}/complete?paymentMethod={Uri.EscapeDataString(paymentMethod ?? "")}";
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), url);
                var response = await _http.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API ERROR] CompleteOrder: {error}");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API EXCEPTION]: {ex.Message}");
                return false;
            }
        }
    }
}