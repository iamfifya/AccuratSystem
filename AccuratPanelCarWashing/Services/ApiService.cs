using AccuratPanelCarWashing.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace AccuratPanelCarWashing.Services
{
    public class ApiService
    {
        private readonly HttpClient _http;

        public ApiService()
        {
            // Базовый адрес. Убедись, что порт совпадает с портом твоего API!
            _http = new HttpClient { BaseAddress = new Uri("https://localhost:7165/api/") };
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
            // Используем SendAsync для PATCH, так как в старых версиях HttpClient нет PatchAsync
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
        public async Task<List<CarWashOrder>> GetOrdersAsync()
        {
            try { return await _http.GetFromJsonAsync<List<CarWashOrder>>("Orders") ?? new List<CarWashOrder>(); }
            catch (HttpRequestException ex) { throw new Exception("Ошибка при получении списка заказов: " + ex.Message); }
        }

        public async Task<List<CarWashOrder>> GetOrdersByClientIdAsync(int clientId)
        {
            try { return await _http.GetFromJsonAsync<List<CarWashOrder>>($"Orders/client/{clientId}") ?? new List<CarWashOrder>(); }
            catch (HttpRequestException ex) { throw new Exception($"История заказов клиента: {ex.Message}"); }
        }

        public async Task<CarWashOrder> CreateOrderAsync(CarWashOrder order)
        {
            var response = await _http.PostAsJsonAsync("Orders", order);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CarWashOrder>();
        }

        public async Task UpdateOrderAsync(CarWashOrder order)
        {
            var response = await _http.PutAsJsonAsync($"Orders/{order.Id}", order);
            response.EnsureSuccessStatusCode();
        }
        #endregion

        // В ApiService.cs, в регион #region ЗАКАЗЫ (ORDERS) или создайте новый

        #region ПРОВЕРКА ДОСТУПНОСТИ БОКСА (для записей)
        public async Task<bool> CheckBoxAvailabilityForAppointmentAsync(int box, DateTime startTime, int durationMinutes)
        {
            try
            {
                // Загружаем все заказы и фильтруем локально
                var allOrders = await GetOrdersAsync();

                var endTime = startTime.AddMinutes(durationMinutes);

                // Проверяем пересечения: только активные записи на том же боксе
                var conflicts = allOrders.Where(o =>
                    o.IsAppointment &&
                    o.BoxNumber == box &&
                    o.Status != "Отменен" &&
                    o.Status != "Выполнен" &&
                    // Пересечение временных интервалов
                    o.Time < endTime &&
                    o.Time.AddMinutes(60) > startTime // 60 мин — средняя длительность, можно сделать параметром
                ).ToList();

                return !conflicts.Any();
            }
            catch
            {
                // При ошибке считаем, что время свободно (или верните false для безопасности)
                return true;
            }
        }
        #endregion

        #region Авторизация с привзкой к филиалу
        public async Task<List<Branch>> GetBranchesAsync()
        {
            try { return await _http.GetFromJsonAsync<List<Branch>>("Branches") ?? new List<Branch>(); }
            catch { return new List<Branch>(); }
        }

        public async Task<User> AuthenticateAsync(string login, string password)
        {
            var response = await _http.PostAsJsonAsync("Users/login", new { Login = login, Password = password });
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<User>();
            return null;
        }
        #endregion

        #region ФИНАНСЫ (TRANSACTIONS)

        // Получаем транзакции только для конкретного филиала
        public async Task<List<Transaction>> GetTransactionsByBranchAsync(int branchId)
        {
            try
            {
                return await _http.GetFromJsonAsync<List<Transaction>>($"Transactions/branch/{branchId}") ?? new List<Transaction>();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Финансы (Transactions): {ex.Message}");
            }
        }

        public async Task<Transaction> CreateTransactionAsync(Transaction transaction)
        {
            var response = await _http.PostAsJsonAsync("Transactions", transaction);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Transaction>();
        }

        public class CashboxSummary { public decimal CashInHand { get; set; } public decimal TotalExpenses { get; set; } public decimal NetCashProfit { get; set; } }

        // В сам класс ApiService добавь:
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
        public async Task<List<ShiftReport>> GetShiftReportsAsync(DateTime start, DateTime end)
        {
            // ДОБАВЛЯЕМ BRANCH ID В ЗАПРОС
            int branchId = AppSettings.CurrentBranchId;
            try { return await _http.GetFromJsonAsync<List<ShiftReport>>($"Reports/shifts?branchId={branchId}&start={start:O}&end={end:O}") ?? new List<ShiftReport>(); }
            catch { return new List<ShiftReport>(); }
        }

        public class ClientStatsResponse { public int NewClients { get; set; } public int UniqueClients { get; set; } }

        public async Task<ClientStatsResponse> GetClientsStatsAsync(DateTime start, DateTime end)
        {
            // ДОБАВЛЯЕМ BRANCH ID В ЗАПРОС
            int branchId = AppSettings.CurrentBranchId;
            try { return await _http.GetFromJsonAsync<ClientStatsResponse>($"Reports/clients-stats?branchId={branchId}&start={start:O}&end={end:O}") ?? new ClientStatsResponse(); }
            catch { return new ClientStatsResponse(); }
        }

        public async Task<List<Transaction>> GetTransactionsByDateRangeAsync(DateTime start, DateTime end)
        {
            int branchId = AppSettings.CurrentBranchId;
            try
            {
                // ПУТЬ ДОЛЖЕН БЫТЬ "Transactions/range"
                return await _http.GetFromJsonAsync<List<Transaction>>($"Transactions/range?branchId={branchId}&start={start:O}&end={end:O}") ?? new List<Transaction>();
            }
            catch
            {
                return new List<Transaction>();
            }
        }
        #endregion

        #region ГРАФИКИ (SCHEDULES) И КОНВЕРТАЦИЯ
        public async Task<List<EmployeeSchedule>> GetScheduleAsync(int year, int month)
        {
            try { return await _http.GetFromJsonAsync<List<EmployeeSchedule>>($"Schedules/{year}/{month}") ?? new List<EmployeeSchedule>(); }
            catch { return new List<EmployeeSchedule>(); }
        }

        public async Task SaveScheduleAsync(int year, int month, List<EmployeeSchedule> scheduleData)
        {
            var response = await _http.PostAsJsonAsync($"Schedules/{year}/{month}", scheduleData);
            response.EnsureSuccessStatusCode();
        }

        public async Task<CarWashOrder> ConvertAppointmentToOrderAsync(int appointmentId, int shiftId, int washerId)
        {
            var response = await _http.PostAsync($"Appointments/{appointmentId}/convert?shiftId={shiftId}&washerId={washerId}", null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CarWashOrder>();
        }
        #endregion
    }
}
