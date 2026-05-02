using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyPanelCarWashing.Models;
using System.Threading.Tasks;

namespace MyPanelCarWashing.Services
{
    public class DataService
    {
        private AppData _data;

        public DataService()
        {
            try
            {
                Logger.Info("Инициализация DataService", "CORE");
                LoadData();

                Logger.Info("Запуск проверки и исправления данных", "CORE");
                CleanupDuplicateServices();
                CleanupInvalidData();
                FixInvalidData();
                RecalculateAllClientsStats();
                _data.UpdateIds();

                FileDataService.CleanupOldBackups(7);
                Logger.Info("DataService успешно инициализирован", "CORE", $"Пользователей: {_data.Users.Count}, Смен: {_data.Shifts.Count}");
            }
            catch (Exception ex)
            {
                Logger.Error("Критическая ошибка при инициализации DataService", ex, "CORE");
            }
        }

        private void LoadData()
        {
            _data = FileDataService.LoadData();
            _data.UpdateIds();
        }

        public void SaveData()
        {
            FileDataService.SaveData(_data);
        }
        /// <summary>
        /// Исправляет некорректные данные (удаляет ссылки на несуществующих сотрудников)
        /// </summary>
        public void FixInvalidData()
        {
            LoadData();
            bool needSave = false;
            var allUsers = _data.Users.Select(u => u.Id).ToHashSet();

            foreach (var shift in _data.Shifts)
            {
                if (shift.Orders != null)
                {
                    foreach (var order in shift.Orders)
                    {
                        // Если мойщик не существует, сбрасываем WasherId на 0 или первого доступного
                        if (order.WasherId > 0 && !allUsers.Contains(order.WasherId))
                        {
                            System.Diagnostics.Debug.WriteLine($"Исправлен заказ #{order.Id}: WasherId {order.WasherId} -> 0");
                            order.WasherId = 0;
                            needSave = true;
                        }
                    }
                }
            }

            if (needSave)
            {
                SaveData();
                System.Diagnostics.Debug.WriteLine("Некорректные данные исправлены");
            }
        }

        // Получить только активных пользователей
        public List<User> GetAllActiveUsers()
        {
            return _data.Users.Where(u => u.IsActive).ToList();
        }

        // Получить всех (включая неактивных) для администрирования
        public List<User> GetAllUsersIncludingInactive()
        {
            return _data.Users.ToList();
        }

        // Получить всех для аутентификации и других нужд
        public List<User> GetAllUsers()
        {
            return _data.Users.Where(u => u.IsActive).ToList();
        }
        public void UpdateUser(User user)
        {
            var existing = _data.Users.FirstOrDefault(u => u.Id == user.Id);
            if (existing != null)
            {
                string oldPhone = existing.Phone ?? "не указан";
                string oldRole = existing.IsAdmin ? "Админ" : "Сотрудник";

                existing.FullName = user.FullName;
                existing.Phone = user.Phone;
                existing.Login = user.Login;
                existing.IsAdmin = user.IsAdmin;
                existing.IsActive = user.IsActive;
                if (!string.IsNullOrWhiteSpace(user.Password))
                {
                    existing.Password = user.Password;
                }
                SaveData();

                Logger.Info($"Сотрудник обновлён | ID: {user.Id} | ФИО: {user.FullName} | Тел: {oldPhone}→{user.Phone ?? "не указан"} | Роль: {oldRole}→{(user.IsAdmin ? "Админ" : "Сотрудник")}", "USER");
            }
        }

        public void AddOrderTransactional(CarWashOrder order, List<int> serviceIds)
        {
            TransactionService.ExecuteInTransaction(appData =>
            {
                var shift = appData.Shifts.FirstOrDefault(s => s.Id == order.ShiftId);
                if (shift == null)
                    throw new Exception("Смена не найдена");

                order.Id = GetNextOrderId(appData);
                order.ServiceIds = serviceIds;
                order.TotalPrice = appData.Services
                    .Where(s => serviceIds.Contains(s.Id))
                    .Sum(s => s.GetPrice(order.BodyTypeCategory));

                if (shift.Orders == null) shift.Orders = new List<CarWashOrder>();
                shift.Orders.Add(order);

                // Обновляем статистику клиента
                if (order.ClientId.HasValue)
                {
                    var client = appData.Clients.FirstOrDefault(c => c.Id == order.ClientId.Value);
                    if (client != null)
                    {
                        client.VisitsCount++;
                        client.TotalSpent += order.FinalPrice;
                        client.LastVisitDate = DateTime.Now;
                    }
                }

                return true;
            });
        }

        public CarWashOrder ConvertAppointmentToOrderTransactional(Appointment appointment, int shiftId, int washerId)
        {
            return TransactionService.ExecuteInTransaction(appData =>
            {
                var shift = appData.Shifts.FirstOrDefault(s => s.Id == shiftId);
                if (shift == null)
                    throw new Exception("Смена не найдена");

                var app = appData.Appointments.FirstOrDefault(a => a.Id == appointment.Id);
                if (app == null)
                    throw new Exception("Запись не найдена");

                var selectedServices = appData.Services.Where(s => app.ServiceIds.Contains(s.Id)).ToList();
                decimal totalPrice = selectedServices.Sum(s => s.GetPrice(app.BodyTypeCategory));

                var order = new CarWashOrder
                {
                    Id = GetNextOrderId(appData),
                    CarNumber = app.CarNumber,
                    CarModel = app.CarModel,
                    CarBodyType = app.CarBodyType,
                    BodyTypeCategory = app.BodyTypeCategory,
                    Time = app.AppointmentDate,
                    ShiftId = shiftId,
                    BoxNumber = app.BoxNumber,
                    WasherId = washerId,
                    ServiceIds = app.ServiceIds.ToList(),
                    ExtraCost = app.ExtraCost,
                    ExtraCostReason = app.ExtraCostReason,
                    Notes = app.Notes,
                    Status = "В ожидании",
                    TotalPrice = totalPrice,
                    IsAppointment = true,
                    AppointmentId = app.Id
                };

                shift.Orders.Add(order);
                app.IsCompleted = true;
                app.OrderId = order.Id;

                // Обновляем статистику клиента
                if (order.ClientId.HasValue)
                {
                    var client = appData.Clients.FirstOrDefault(c => c.Id == order.ClientId.Value);
                    if (client != null)
                    {
                        client.VisitsCount++;
                        client.TotalSpent += order.FinalPrice;
                        client.LastVisitDate = DateTime.Now;
                    }
                }

                return order;
            });
        }

        public static event Action DataChanged;

        public static void NotifyDataChanged()
        {
            DataChanged?.Invoke();
        }

        // Пользователи
        public User AuthenticateUser(string login, string password)
        {
            return _data.Users.FirstOrDefault(u => u.Login == login && u.Password == password);
        }

        public void AddUser(User user)
        {
            user.Id = _data.GetNextUserId();
            _data.Users.Add(user);
            SaveData();

            Logger.Info($"Сотрудник добавлен | ФИО: {user.FullName} | Логин: {user.Login} | Тел: {user.Phone ?? "не указан"} | Роль: {(user.IsAdmin ? "Админ" : "Сотрудник")}", "USER");
        }

        

        // Услуги
        public List<Service> GetAllServices()
        {
            return _data.Services.Where(s => s.IsActive).ToList();
        }

        // Смены
        public Shift GetShiftByDate(DateTime date)
        {
            // Используем только дату, игнорируя время
            var targetDate = date.Date;

            System.Diagnostics.Debug.WriteLine($"GetShiftByDate ищет смену на {targetDate:dd.MM.yyyy}");

            // Выводим все смены для отладки
            foreach (var s in _data.Shifts)
            {
                System.Diagnostics.Debug.WriteLine($"  Смена: ID={s.Id}, Дата={s.Date:dd.MM.yyyy}, IsClosed={s.IsClosed}");
            }

            var openShift = _data.Shifts.FirstOrDefault(s => s.Date.Date == targetDate && !s.IsClosed);

            System.Diagnostics.Debug.WriteLine($"Результат: {(openShift != null ? $"найдена ID={openShift.Id}" : "не найдена")}");

            return openShift;
        }

        public List<Shift> GetAllShifts()
        {
            return _data.Shifts.OrderByDescending(s => s.Date).ToList();
        }

        public void CloseShift(int shiftId, string notes)
        {
            var shift = _data.Shifts.FirstOrDefault(s => s.Id == shiftId);
            if (shift != null && !shift.IsClosed)
            {
                shift.EndTime = DateTime.Now;
                shift.IsClosed = true;
                shift.Notes = notes;
                SaveData();
            }
        }

        public CarWashOrder GetOrderByAppointmentId(int appointmentId)
        {
            var appData = FileDataService.LoadData();
            foreach (var shift in appData.Shifts)
            {
                var order = shift.Orders?.FirstOrDefault(o => o.AppointmentId == appointmentId);
                if (order != null)
                    return order;
            }
            return null;
        }

        // Заказы
        public void AddOrder(CarWashOrder order, List<int> serviceIds)
        {
            // ВАЖНО: перезагружаем данные перед добавлением
            LoadData();

            FileDataService.CreateAutoBackup();

            var shift = _data.Shifts.FirstOrDefault(s => s.Id == order.ShiftId);

            if (shift != null)
            {
                if (shift.Orders == null) shift.Orders = new List<CarWashOrder>();

                // ПРОВЕРКА: нет ли уже такого заказа?
                if (shift.Orders.Any(o => o.Id == order.Id && order.Id > 0))
                {
                    System.Diagnostics.Debug.WriteLine($"Заказ ID {order.Id} уже существует, пропускаем добавление");
                    return;
                }

                order.Id = GetNextOrderId(_data);
                order.ServiceIds = serviceIds;
                order.TotalPrice = _data.Services
                    .Where(s => serviceIds.Contains(s.Id))
                    .Sum(s => s.GetPrice(order.BodyTypeCategory));

                order.OriginalTotalPrice = order.TotalPrice;
                order.DiscountPercent = order.DiscountPercent;
                order.DiscountAmount = order.DiscountAmount;

                shift.Orders.Add(order);

                if (order.ClientId.HasValue)
                {
                    RecalculateClientStats(order.ClientId.Value);
                }

                SaveData();
                NotifyDataChanged();
            }
        }

        public void RecalculateClientStats(int clientId)
        {
            var appData = FileDataService.LoadData();
            var client = appData.Clients.FirstOrDefault(c => c.Id == clientId);
            if (client == null) return;

            // Находим все выполненные заказы клиента
            var completedOrders = appData.Shifts
                .SelectMany(s => s.Orders ?? new List<CarWashOrder>())
                .Where(o => o.ClientId == clientId && o.Status == "Выполнен")
                .ToList();

            // Обновляем статистику
            client.VisitsCount = completedOrders.Count;
            client.TotalSpent = completedOrders.Sum(o => o.FinalPrice);
            client.LastVisitDate = completedOrders.Any() ? completedOrders.Max(o => o.Time) : (DateTime?)null;

            FileDataService.SaveData(appData);

            System.Diagnostics.Debug.WriteLine($"=== ПЕРЕСЧЕТ СТАТИСТИКИ ===");
            System.Diagnostics.Debug.WriteLine($"Клиент: {client.FullName}");
            System.Diagnostics.Debug.WriteLine($"Найдено выполненных заказов: {completedOrders.Count}");
            System.Diagnostics.Debug.WriteLine($"Общая сумма: {client.TotalSpent}");
        }

        public void RecalculateAllClientsStats()
        {
            var appData = FileDataService.LoadData();
            foreach (var client in appData.Clients)
            {
                var completedOrders = appData.Shifts
                    .SelectMany(s => s.Orders ?? new List<CarWashOrder>())
                    .Where(o => o.ClientId == client.Id && o.Status == "Выполнен")
                    .ToList();

                client.VisitsCount = completedOrders.Count;
                client.TotalSpent = completedOrders.Sum(o => o.FinalPrice);
                client.LastVisitDate = completedOrders.Any() ? completedOrders.Max(o => o.Time) : (DateTime?)null;
            }
            FileDataService.SaveData(appData);
            System.Diagnostics.Debug.WriteLine($"Пересчитана статистика для {appData.Clients.Count} клиентов");
        }

        public void UpdateOrderServices(int orderId, List<int> serviceIds)
        {
            var order = GetOrderById(orderId);
            if (order != null)
            {
                order.ServiceIds = serviceIds;
                order.TotalPrice = _data.Services
                    .Where(s => serviceIds.Contains(s.Id))
                    .Sum(s => s.GetPrice(order.BodyTypeCategory)); // Используем GetPrice
                SaveData();
            }
        }
        public CarWashOrder GetOrderById(int orderId)
        {
            try
            {
                var appData = FileDataService.LoadData();
                foreach (var shift in appData.Shifts)
                {
                    var order = shift.Orders?.FirstOrDefault(o => o.Id == orderId);
                    if (order != null)
                        return order;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public List<int> GetOrderServiceIds(int orderId)
        {
            var order = GetOrderById(orderId);
            return order?.ServiceIds ?? new List<int>();
        }

        // DataService.cs - добавьте эти методы

        public void UpdateAppointment(Appointment appointment)
        {
            try
            {
                var appData = FileDataService.LoadData();
                var existing = appData.Appointments.FirstOrDefault(a => a.Id == appointment.Id);

                if (existing != null)
                {
                    existing.CarModel = appointment.CarModel;
                    existing.CarNumber = appointment.CarNumber;
                    existing.CarBodyType = appointment.CarBodyType;
                    existing.AppointmentDate = appointment.AppointmentDate;
                    existing.DurationMinutes = appointment.DurationMinutes;
                    existing.BoxNumber = appointment.BoxNumber;
                    existing.ServiceIds = appointment.ServiceIds;
                    existing.ExtraCost = appointment.ExtraCost;
                    existing.ExtraCostReason = appointment.ExtraCostReason;
                    existing.Notes = appointment.Notes;
                    existing.IsCompleted = appointment.IsCompleted;
                }

                FileDataService.SaveData(appData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка UpdateAppointment: {ex.Message}");
                throw;
            }
        }

        public void UpdateOrder(CarWashOrder order)
        {
            try
            {
                var appData = FileDataService.LoadData();
                var shift = appData.Shifts.FirstOrDefault(s => s.Id == order.ShiftId);

                if (shift != null)
                {
                    var existingOrder = shift.Orders.FirstOrDefault(o => o.Id == order.Id);
                    if (existingOrder != null)
                    {
                        // Обновляем ВСЕ поля, включая скидку
                        existingOrder.CarModel = order.CarModel;
                        existingOrder.CarNumber = order.CarNumber;
                        existingOrder.CarBodyType = order.CarBodyType;
                        existingOrder.BodyTypeCategory = order.BodyTypeCategory;
                        existingOrder.Time = order.Time;
                        existingOrder.BoxNumber = order.BoxNumber;
                        existingOrder.WasherId = order.WasherId;
                        existingOrder.ServiceIds = order.ServiceIds;
                        existingOrder.ExtraCost = order.ExtraCost;
                        existingOrder.ExtraCostReason = order.ExtraCostReason;
                        existingOrder.TotalPrice = order.TotalPrice;
                        existingOrder.Status = order.Status;
                        existingOrder.PaymentMethod = order.PaymentMethod;
                        existingOrder.Notes = order.Notes;

                        // === ДОБАВЛЕНО: сохраняем скидку ===
                        existingOrder.DiscountPercent = order.DiscountPercent;
                        existingOrder.DiscountAmount = order.DiscountAmount;
                        existingOrder.OriginalTotalPrice = order.OriginalTotalPrice;
                        // === КОНЕЦ ДОБАВЛЕНИЯ ===
                    }
                }

                FileDataService.SaveData(appData);

                // Оповещаем об изменении
                NotifyDataChanged();

                System.Diagnostics.Debug.WriteLine($"[SAVE] Order #{order.Id} | Discount%: {order.DiscountPercent} | Discount$: {order.DiscountAmount}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка UpdateOrder: {ex.Message}");
                throw;
            }
        }

        private void MigrateOldServices()
        {
            bool needSave = false;

            foreach (var service in _data.Services)
            {
                // Если у услуги есть старый Price и нет PriceByBodyType
                if (service.PriceByBodyType == null || !service.PriceByBodyType.Any())
                {
                    service.PriceByBodyType = new Dictionary<int, decimal>();

                    // Если есть старая цена, используем её для всех категорий
                    // (через рефлексию или временное поле)
                    // В вашем случае у Service уже есть Price? Проверьте.
                }
            }

            if (needSave)
                SaveData();
        }

        public void AddEmployeeToShift(int shiftId, int userId)
        {
            var shift = _data.Shifts.FirstOrDefault(s => s.Id == shiftId);
            if (shift != null && !shift.EmployeeIds.Contains(userId))
            {
                shift.EmployeeIds.Add(userId);
                SaveData();
            }
        }

        // Отчеты
        public List<MonthlyReport> GetMonthlyReport(int year, int month)
        {
            var shifts = _data.Shifts
                .Where(s => s.Date.Year == year && s.Date.Month == month && s.IsClosed)
                .ToList();

            return new List<MonthlyReport>
            {
                new MonthlyReport
                {
                    Year = year,
                    Month = month,
                    TotalCars = shifts.Sum(s => s.TotalCars),
                    TotalRevenue = shifts.Sum(s => s.TotalRevenue)
                }
            };
        }
        // Добавить в класс DataService
        public bool IsShiftStarted(DateTime date)
        {
            return _data.Shifts.Any(s => s.Date.Date == date.Date && !s.IsClosed);
        }

        public Shift GetActiveShift()
        {
            return _data.Shifts.FirstOrDefault(s => !s.IsClosed);
        }

        public void StartShift(DateTime date, List<int> employeeIds)
        {
            System.Diagnostics.Debug.WriteLine($"StartShift вызван для даты {date:dd.MM.yyyy}");

            // Удаляем существующую открытую смену на эту дату
            var existingShift = _data.Shifts.FirstOrDefault(s => s.Date.Date == date.Date && !s.IsClosed);
            if (existingShift != null)
            {
                System.Diagnostics.Debug.WriteLine($"  Удаляем существующую смену ID={existingShift.Id}");
                _data.Shifts.Remove(existingShift);
            }

            // Создаем новую смену
            var shift = new Shift
            {
                Id = _data.GetNextShiftId(),
                Date = date,
                StartTime = DateTime.Now,
                IsClosed = false,
                EmployeeIds = employeeIds,
                Orders = new List<CarWashOrder>()
            };

            System.Diagnostics.Debug.WriteLine($"  Создаем новую смену ID={shift.Id}, Дата={shift.Date:dd.MM.yyyy}");
            _data.Shifts.Add(shift);
            SaveData();

            // Проверяем, что сохранилось
            var saved = _data.Shifts.FirstOrDefault(s => s.Id == shift.Id);
            System.Diagnostics.Debug.WriteLine($"  После сохранения: {(saved != null ? $"OK, ID={saved.Id}, Дата={saved.Date:dd.MM.yyyy}" : "НЕ СОХРАНИЛОСЬ!")}");
        }
        public Shift GetShiftById(int shiftId)
        {
            return _data.Shifts.FirstOrDefault(s => s.Id == shiftId);
        }
        public async Task<List<Service>> GetAllServicesAsync()
        {
            return await Task.Run(() => _data.Services.Where(s => s.IsActive).ToList());
        }

        public async Task<Shift> GetShiftByDateAsync(DateTime date)
        {
            return await Task.Run(() => _data.Shifts.FirstOrDefault(s => s.Date.Date == date.Date && !s.IsClosed));
        }

        public async Task AddOrderAsync(CarWashOrder order, List<int> serviceIds)
        {
            await Task.Run(() => AddOrder(order, serviceIds));
        }
        // Получить все записи
        public List<Appointment> GetAllAppointments()
        {
            return _data.Appointments.OrderBy(a => a.AppointmentDate).ToList();
        }

        // Получить записи на конкретную дату
        public List<Appointment> GetAppointmentsByDate(DateTime date)
        {
            return _data.Appointments
                .Where(a => a.AppointmentDate.Date == date.Date && !a.IsCompleted)
                .OrderBy(a => a.AppointmentDate)
                .ToList();
        }

        public bool IsBoxAvailable(int boxNumber, DateTime startTime, int durationMinutes)
        {
            var endTime = startTime.AddMinutes(durationMinutes);

            // Получаем ВСЕ записи на эту дату для конкретного бокса
            var appointments = _data.Appointments
                .Where(a => !a.IsCompleted &&
                            a.AppointmentDate.Date == startTime.Date &&
                            a.BoxNumber == boxNumber)  // ← Проверяем конкретный бокс
                .ToList();

            // Проверяем пересечения
            foreach (var a in appointments)
            {
                var aStart = a.AppointmentDate;
                var aEnd = a.EndTime;

                // Если интервалы пересекаются
                if (endTime > aStart && startTime < aEnd)
                {
                    return false;
                }
            }

            return true;
        }

        public void ClearAllData()
        {
            _data.Shifts.Clear();
            _data.Appointments.Clear();
            SaveData();
            System.Diagnostics.Debug.WriteLine("Все данные очищены");
        }

        // DataService.cs - добавьте эти методы

        public List<EmployeeSchedule> GetSchedule(int year, int month)
        {
            var appData = FileDataService.LoadData();
            var schedule = appData.Schedules?.FirstOrDefault(s => s.Year == year && s.Month == month);

            if (schedule != null)
            {
                System.Diagnostics.Debug.WriteLine($"Найден график: {schedule.Year}.{schedule.Month}, сотрудников: {schedule.EmployeeSchedules.Count}");
                foreach (var emp in schedule.EmployeeSchedules)
                {
                    System.Diagnostics.Debug.WriteLine($"  {emp.EmployeeName} - {emp.Position}, дней: {emp.Days.Count}");
                }
                return schedule.EmployeeSchedules;
            }

            System.Diagnostics.Debug.WriteLine($"График на {year}.{month} не найден");
            return new List<EmployeeSchedule>();
        }

        public void SaveSchedule(int year, int month, List<EmployeeSchedule> scheduleData)
        {
            var appData = FileDataService.LoadData();

            // Инициализируем список, если null
            if (appData.Schedules == null)
                appData.Schedules = new List<Schedule>();

            // Ищем существующий график
            var existingSchedule = appData.Schedules.FirstOrDefault(s => s.Year == year && s.Month == month);

            if (existingSchedule != null)
            {
                // Обновляем существующий
                existingSchedule.EmployeeSchedules = scheduleData;
                System.Diagnostics.Debug.WriteLine($"Обновлен график на {year}.{month}");
            }
            else
            {
                // Создаем новый
                int newId = appData.Schedules.Any() ? appData.Schedules.Max(s => s.Id) + 1 : 1;
                appData.Schedules.Add(new Schedule
                {
                    Id = newId,
                    Year = year,
                    Month = month,
                    EmployeeSchedules = scheduleData
                });
                System.Diagnostics.Debug.WriteLine($"Создан новый график на {year}.{month}, ID: {newId}");
            }

            FileDataService.SaveData(appData);
        }

        private void CleanupInvalidData()
        {
            // Удаляем заказы, у которых IsAppointment = true, но нет соответствующей записи
            var invalidOrders = _data.Shifts
                .SelectMany(s => s.Orders)
                .Where(o => o.IsAppointment && !_data.Appointments.Any(a => a.Id == o.AppointmentId))
                .ToList();

            foreach (var order in invalidOrders)
            {
                var shift = _data.Shifts.FirstOrDefault(s => s.Orders.Contains(order));
                if (shift != null)
                {
                    shift.Orders.Remove(order);
                    System.Diagnostics.Debug.WriteLine($"Удален некорректный заказ ID={order.Id}");
                }
            }

            // Удаляем пустые смены
            _data.Shifts.RemoveAll(s => s.Orders.Count == 0 && s.IsClosed);

            if (invalidOrders.Any())
            {
                SaveData();
            }
        }


        // Добавить запись
        public void AddAppointment(Appointment appointment)
        {
            LoadData();

            if (!IsBoxAvailable(appointment.BoxNumber, appointment.AppointmentDate, appointment.DurationMinutes))
            {
                Logger.Warn($"Попытка создать запись на занятое время | Бокс: {appointment.BoxNumber} | Время: {appointment.AppointmentDate:HH:mm}", "APPOINTMENT");
                throw new Exception("Выбранное время уже занято!");
            }

            FileDataService.CreateAutoBackup();

            appointment.Id = _data.GetNextAppointmentId();
            _data.Appointments.Add(appointment);
            SaveData();

            Logger.Info($"Запись создана | Авто: {appointment.CarNumber} | Время: {appointment.AppointmentDate:dd.MM.yyyy HH:mm} | Бокс: {appointment.BoxNumber} | Услуг: {appointment.ServiceIds.Count}", "APPOINTMENT");

            NotifyDataChanged();
        }

        // Удалить запись
        public void DeleteAppointment(int appointmentId)
        {
            var appointment = _data.Appointments.FirstOrDefault(a => a.Id == appointmentId);
            if (appointment != null)
            {
                string info = $"Авто: {appointment.CarNumber} | Время: {appointment.AppointmentDate:HH:mm}";
                _data.Appointments.Remove(appointment);
                SaveData();
                Logger.Info($"Запись удалена | {info}", "APPOINTMENT");
            }
        }

        // Преобразовать запись в заказ при наступлении смены
        public CarWashOrder ConvertAppointmentToOrder(Appointment appointment, int shiftId, int washerId)
        {
            var selectedServices = _data.Services.Where(s => appointment.ServiceIds.Contains(s.Id)).ToList();
            decimal totalPrice = selectedServices.Sum(s => s.GetPrice(appointment.BodyTypeCategory));

            var order = new CarWashOrder
            {
                Id = _data.GetNextOrderId(),
                CarNumber = appointment.CarNumber,
                CarModel = appointment.CarModel,
                CarBodyType = appointment.CarBodyType,
                BodyTypeCategory = appointment.BodyTypeCategory,
                Time = appointment.AppointmentDate,
                ShiftId = shiftId,
                BoxNumber = appointment.BoxNumber,
                WasherId = washerId,
                ServiceIds = appointment.ServiceIds.ToList(),
                ExtraCost = appointment.ExtraCost,
                ExtraCostReason = appointment.ExtraCostReason,
                Notes = appointment.Notes,
                Status = "Выполняется", // ← ИСПРАВЛЕНО: было "В ожидании"
                TotalPrice = totalPrice,
                IsAppointment = true,
                AppointmentId = appointment.Id
            };

            appointment.IsCompleted = true;
            appointment.OrderId = order.Id;
            SaveData();

            // Оповещаем об изменении
            NotifyDataChanged();

            return order;
        }
        public Appointment GetAppointmentById(int appointmentId)
        {
            return _data.Appointments.FirstOrDefault(a => a.Id == appointmentId);
        }
        public void UpdateOrderStatus(int orderId, string status)
        {
            var order = GetOrderById(orderId);
            if (order != null)
            {
                string oldStatus = order.Status; // ← ОБЯЗАТЕЛЬНО объявляем ДО изменения
                order.Status = status;
                SaveData();

                if (status == "Выполнен" && oldStatus != "Выполнен" && order.ClientId.HasValue)
                {
                    UpdateClientLoyalty(order.ClientId.Value); // ← Вызов метода
                }
            }
        }
        private int GetNextOrderId(AppData appData)
        {
            int maxId = 0;
            foreach (var shift in appData.Shifts)
            {
                if (shift.Orders != null && shift.Orders.Any())
                {
                    var maxInShift = shift.Orders.Max(o => o.Id);
                    if (maxInShift > maxId) maxId = maxInShift;
                }
            }
            return maxId + 1;
        }
        public Shift GetCurrentOpenShift()
        {
            try
            {
                var appData = FileDataService.LoadData();
                return appData.Shifts?.FirstOrDefault(s => !s.IsClosed && s.Date.Date == DateTime.Now.Date);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCurrentOpenShift error: {ex.Message}");
                return null;
            }
        }

        private void CleanupDuplicateServices()
        {
            // Находим дубликаты по ID
            var duplicateIds = _data.Services
                .GroupBy(s => s.Id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateIds.Any())
            {
                System.Diagnostics.Debug.WriteLine($"Найдены дубликаты ID: {string.Join(", ", duplicateIds)}");

                // Группируем по ID и оставляем только первую запись
                var cleanedServices = _data.Services
                    .GroupBy(s => s.Id)
                    .Select(g => g.First())
                    .OrderBy(s => s.Id)
                    .ToList();

                _data.Services = cleanedServices;

                // Находим дубликаты по названию
                var duplicateNames = _data.Services
                    .GroupBy(s => s.Name)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateNames.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"Найдены дубликаты названий: {string.Join(", ", duplicateNames)}");

                    // Оставляем только одну услугу с каждым названием
                    cleanedServices = _data.Services
                        .GroupBy(s => s.Name)
                        .Select(g => g.First())
                        .OrderBy(s => s.Id)
                        .ToList();

                    _data.Services = cleanedServices;
                }

                // Перенумеровываем ID
                int newId = 1;
                foreach (var service in _data.Services.OrderBy(s => s.Id))
                {
                    service.Id = newId++;
                }

                SaveData();
                System.Diagnostics.Debug.WriteLine($"Дубликаты удалены. Осталось услуг: {_data.Services.Count}");
            }
        }
        public void AddService(Service service)
        {
            try
            {
                // === 1. Перезагружаем данные для актуального списка ===
                LoadData();

                // === 2. НАДЁЖНЫЙ расчёт следующего ID ===
                int newId = 1; // Значение по умолчанию, если услуг нет
                if (_data.Services != null && _data.Services.Any())
                {
                    newId = _data.Services.Max(s => s.Id) + 1;
                }

                // === 3. ПРИНУДИТЕЛЬНО присваиваем новый ID (игнорируем входящий!) ===
                service.Id = newId;

                // === 4. Защита от гонки: если вдруг такой ID уже есть — ищем следующий свободный ===
                while (_data.Services.Any(s => s.Id == service.Id))
                {
                    service.Id++;
                }

                // === 5. Добавляем и сохраняем ===
                if (_data.Services == null)
                    _data.Services = new List<Service>();

                _data.Services.Add(service);
                SaveData();

                // === 6. Логирование ===
                string prices = string.Join(", ", service.PriceByBodyType?.Select(p => $"Кат.{p.Key}: {p.Value}₽") ?? new[] { "нет цен" });
                Logger.Info($"Услуга добавлена | Название: {service.Name} | ID: {service.Id} | Цены: {prices}", "SERVICE");

                // === 7. Уведомление об изменении ===
                NotifyDataChanged();
            }
            catch (Exception ex)
            {
                Logger.Error("Ошибка при добавлении услуги", ex, "SERVICE");
                throw;
            }
        }
        public void UpdateService(Service service)
        {
            var appData = FileDataService.LoadData();
            var existing = appData.Services.FirstOrDefault(s => s.Id == service.Id);
            if (existing != null)
            {
                // Сохраняем старые цены для сравнения
                string oldPrices = string.Join(", ", existing.PriceByBodyType.Select(p => $"Кат.{p.Key}: {p.Value}₽"));
                string newPrices = string.Join(", ", service.PriceByBodyType.Select(p => $"Кат.{p.Key}: {p.Value}₽"));

                // Проверяем, изменились ли цены
                bool pricesChanged = oldPrices != newPrices;
                string priceLog = pricesChanged ? $" | Цены: {oldPrices} → {newPrices}" : "";

                existing.Name = service.Name;
                existing.IsActive = service.IsActive;
                existing.PriceByBodyType = service.PriceByBodyType;

                FileDataService.SaveData(appData);

                NotifyDataChanged();

                // Логируем изменение
                if (pricesChanged)
                {
                    Logger.Info($"Услуга обновлена (ЦЕНЫ ИЗМЕНЕНЫ) | Название: {service.Name} | ID: {service.Id}{priceLog}", "SERVICE_PRICE");
                }
                else
                {
                    Logger.Info($"Услуга обновлена | Название: {service.Name} | ID: {service.Id}", "SERVICE");
                }
            }
        }
        public void UpdateAppSettings(AppSettings newSettings, AppSettings oldSettings = null)
        {
            try
            {
                string changes = "";

                if (oldSettings != null)
                {
                    if (oldSettings.AutoBackup != newSettings.AutoBackup)
                        changes += $"Автобэкап: {oldSettings.AutoBackup}→{newSettings.AutoBackup}; ";
                    if (oldSettings.BackupDaysToKeep != newSettings.BackupDaysToKeep)
                        changes += $"Дни хранения бэкапов: {oldSettings.BackupDaysToKeep}→{newSettings.BackupDaysToKeep}; ";
                    if (oldSettings.LogDaysToKeep != newSettings.LogDaysToKeep)
                        changes += $"Дни хранения логов: {oldSettings.LogDaysToKeep}→{newSettings.LogDaysToKeep}; ";
                    if (oldSettings.DefaultPaymentMethod != newSettings.DefaultPaymentMethod)
                        changes += $"Способ оплаты по умолчанию: {oldSettings.DefaultPaymentMethod}→{newSettings.DefaultPaymentMethod}; ";
                    if (oldSettings.DefaultWasherPercent != newSettings.DefaultWasherPercent)
                        changes += $"Процент мойщика: {oldSettings.DefaultWasherPercent}→{newSettings.DefaultWasherPercent}%; ";
                    if (oldSettings.RequireConfirmationForDelete != newSettings.RequireConfirmationForDelete)
                        changes += $"Подтверждение удаления: {oldSettings.RequireConfirmationForDelete}→{newSettings.RequireConfirmationForDelete}; ";
                }

                newSettings.Save();

                if (!string.IsNullOrEmpty(changes))
                {
                    Logger.Info($"Настройки приложения изменены | {changes.TrimEnd(';', ' ')}", "SETTINGS");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Ошибка при сохранении настроек", ex, "SETTINGS");
            }
        }
        public string ExportDataToJson(string exportType = "full")
        {
            try
            {
                string exportsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports");
                if (!Directory.Exists(exportsFolder))
                    Directory.CreateDirectory(exportsFolder);

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                string fileName = $"export_{exportType}_{timestamp}.json";
                string filePath = Path.Combine(exportsFolder, fileName);

                var appData = FileDataService.LoadData();
                object exportData = null;

                // Традиционный switch (C# 7.3 совместимый)
                if (exportType == "clients")
                {
                    exportData = new { Clients = appData.Clients, ExportDate = DateTime.Now };
                }
                else if (exportType == "orders")
                {
                    var orders = appData.Shifts.SelectMany(s => s.Orders ?? new List<CarWashOrder>()).ToList();
                    exportData = new { Orders = orders, ExportDate = DateTime.Now };
                }
                else // "full" или любой другой
                {
                    exportData = new
                    {
                        Clients = appData.Clients,
                        Orders = appData.Shifts.SelectMany(s => s.Orders ?? new List<CarWashOrder>()).ToList(),
                        Services = appData.Services,
                        ExportDate = DateTime.Now
                    };
                }

                string json = Newtonsoft.Json.JsonConvert.SerializeObject(exportData, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(filePath, json);

                // Подсчитываем статистику для лога (тоже через if-else)
                string stats = "";
                if (exportType == "clients")
                {
                    stats = $"Клиентов: {appData.Clients.Count}";
                }
                else if (exportType == "orders")
                {
                    stats = $"Заказов: {appData.Shifts.SelectMany(s => s.Orders ?? new List<CarWashOrder>()).Count()}";
                }
                else
                {
                    stats = $"Клиентов: {appData.Clients.Count}, Заказов: {appData.Shifts.SelectMany(s => s.Orders ?? new List<CarWashOrder>()).Count()}, Услуг: {appData.Services.Count}";
                }

                Logger.Info($"ЭКСПОРТ ДАННЫХ | Тип: {exportType} | Файл: {fileName} | {stats}", "EXPORT");

                return filePath;
            }
            catch (Exception ex)
            {
                Logger.Error("Ошибка при экспорте данных", ex, "EXPORT");
                throw;
            }
        }
        /// <summary>
        /// Рассчитывает суммарное рабочее время каждого сотрудника за указанный период
        /// </summary>
        public Dictionary<string, string> GetEmployeeWorkTimeReport(DateTime startDate, DateTime endDate)
        {
            var shifts = _data.Shifts
                .Where(s => s.Date.Date >= startDate.Date && s.Date.Date <= endDate.Date && s.IsClosed && s.EndTime.HasValue)
                .ToList();

            var report = new Dictionary<string, TimeSpan>();

            foreach (var shift in shifts)
            {
                if (shift.EmployeeIds == null || shift.EmployeeIds.Count == 0) continue;

                TimeSpan duration = shift.EndTime.Value - shift.StartTime.Value;
                if (duration.TotalHours < 0) continue; // Защита от некорректных данных

                foreach (int empId in shift.EmployeeIds)
                {
                    var user = _data.Users.FirstOrDefault(u => u.Id == empId);
                    string name = user?.FullName ?? $"Сотрудник ID:{empId}";

                    if (report.ContainsKey(name))
                        report[name] += duration;
                    else
                        report[name] = duration;
                }
            }

            // Преобразуем TimeSpan в читаемый формат "X ч. Y мин."
            var result = new Dictionary<string, string>();
            foreach (var kvp in report)
            {
                int hours = (int)kvp.Value.TotalHours;
                int minutes = kvp.Value.Minutes;
                result[kvp.Key] = $"{hours} ч. {minutes} мин.";
            }

            return result;
        }
        public void DeleteService(int serviceId)
        {
            var appData = FileDataService.LoadData();
            var service = appData.Services.FirstOrDefault(s => s.Id == serviceId);
            if (service != null)
            {
                string info = $"Название: {service.Name} | Цены: {string.Join(", ", service.PriceByBodyType.Select(p => $"Кат.{p.Key}: {p.Value}₽"))}";
                appData.Services.Remove(service);
                FileDataService.SaveData(appData);

                Logger.Warn($"Услуга удалена | {info}", "SERVICE");
            }
        }
        public List<Client> GetAllClients()
        {
            var appData = FileDataService.LoadData();
            return appData.Clients;
        }

        public Client GetClientById(int id)
        {
            var appData = FileDataService.LoadData();
            return appData.Clients.FirstOrDefault(c => c.Id == id);
        }

        public List<Client> FindClients(string searchText)
        {
            var appData = FileDataService.LoadData();
            if (string.IsNullOrWhiteSpace(searchText))
                return appData.Clients;

            return appData.Clients.Where(c =>
                c.FullName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                c.Phone.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                c.CarNumber.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
            ).ToList();
        }

        public void AddClient(Client client)
        {
            var appData = FileDataService.LoadData();
            client.Id = appData.GetNextClientId();
            client.RegistrationDate = DateTime.Now;
            appData.Clients.Add(client);
            FileDataService.SaveData(appData);

            Logger.Info($"Клиент добавлен | ФИО: {client.FullName} | Тел: {client.Phone}", "CLIENT");
        }

        public void UpdateClient(Client client)
        {
            var appData = FileDataService.LoadData();
            var existing = appData.Clients.FirstOrDefault(c => c.Id == client.Id);
            if (existing != null)
            {
                string oldInfo = $"{existing.FullName} | {existing.Phone}";

                existing.FullName = client.FullName;
                existing.Phone = client.Phone;
                existing.CarModel = client.CarModel;
                existing.CarNumber = client.CarNumber;
                existing.Notes = client.Notes;
                existing.DefaultDiscountPercent = client.DefaultDiscountPercent;

                FileDataService.SaveData(appData);

                Logger.Info($"Клиент обновлён | ID: {client.Id} | Было: {oldInfo} | Стало: {client.FullName}", "CLIENT");
            }
        }

        public void DeleteClient(int clientId)
        {
            var appData = FileDataService.LoadData();
            var client = appData.Clients.FirstOrDefault(c => c.Id == clientId);
            if (client != null)
            {
                string info = $"{client.FullName} ({client.Phone}) | Авто: {client.CarNumber}";
                appData.Clients.Remove(client);
                FileDataService.SaveData(appData);
                Logger.Info($"Клиент удалён | {info}", "CLIENT");
            }
        }

        // Обновляем статистику клиента при добавлении заказа
        public void UpdateClientStats(int clientId, decimal orderAmount)
        {
            var appData = FileDataService.LoadData();
            var client = appData.Clients.FirstOrDefault(c => c.Id == clientId);
            if (client != null)
            {
                client.VisitsCount++;
                client.TotalSpent += orderAmount;
                client.LastVisitDate = DateTime.Now;
                FileDataService.SaveData(appData);
            }
        }
        public List<CarWashOrder> GetOrdersByClientId(int clientId)
        {
            var appData = FileDataService.LoadData();
            var orders = new List<CarWashOrder>();

            foreach (var shift in appData.Shifts)
            {
                var clientOrders = shift.Orders?.Where(o => o.ClientId == clientId).ToList();
                if (clientOrders != null && clientOrders.Any())
                {
                    orders.AddRange(clientOrders);
                }
            }

            return orders;
        }
        /// <summary>
        /// Обновляет статистику клиента при изменении статуса заказа
        /// </summary>
        /// <param name="orderId">ID заказа</param>
        /// <param name="oldStatus">Старый статус</param>
        /// <param name="newStatus">Новый статус</param>
        public void UpdateClientStatsOnStatusChange(int orderId, string oldStatus, string newStatus)
        {
            var appData = FileDataService.LoadData();

            CarWashOrder order = null;
            Shift shift = null;

            foreach (var s in appData.Shifts)
            {
                var foundOrder = s.Orders?.FirstOrDefault(o => o.Id == orderId);
                if (foundOrder != null)
                {
                    order = foundOrder;
                    shift = s;
                    break;
                }
            }

            if (order == null || !order.ClientId.HasValue) return;

            var client = appData.Clients.FirstOrDefault(c => c.Id == order.ClientId.Value);
            if (client == null) return;

            // Только статус "Выполнен" влияет на статистику
            bool wasCompleted = oldStatus == "Выполнен";
            bool willBeCompleted = newStatus == "Выполнен";

            // Если статус не менялся или оба не "Выполнен" - ничего не делаем
            if (wasCompleted == willBeCompleted) return;

            if (willBeCompleted && !wasCompleted)
            {
                // Заказ стал выполненным - добавляем статистику
                client.VisitsCount++;
                client.TotalSpent += order.FinalPrice;
                client.LastVisitDate = DateTime.Now;

                System.Diagnostics.Debug.WriteLine($"Статистика клиента {client.FullName} увеличена: +{order.FinalPrice:N0} ₽, визитов: {client.VisitsCount}");
            }
            else if (!willBeCompleted && wasCompleted)
            {
                // Заказ был выполнен, но стал отменен - вычитаем статистику
                client.VisitsCount--;
                client.TotalSpent -= order.FinalPrice;

                // Обновляем дату последнего визита на предыдущий выполненный заказ
                var lastCompletedOrder = shift.Orders
                    .Where(o => o.ClientId == client.Id && o.Id != orderId && o.Status == "Выполнен")
                    .OrderByDescending(o => o.Time)
                    .FirstOrDefault();

                client.LastVisitDate = lastCompletedOrder?.Time ?? client.RegistrationDate;

                System.Diagnostics.Debug.WriteLine($"Статистика клиента {client.FullName} уменьшена: -{order.FinalPrice:N0} ₽, визитов: {client.VisitsCount}");
            }

            FileDataService.SaveData(appData);
        }
        public string CanCloseShift(int shiftId)
        {
            var shift = _data.Shifts.FirstOrDefault(s => s.Id == shiftId);
            if (shift == null)
                return "Смена не найдена";

            if (shift.IsClosed)
                return "Смена уже закрыта";

            // Проверяем заказы со статусом "Выполняется"
            var inProgressOrders = shift.Orders?.Where(o => o.Status == "Выполняется").ToList();
            if (inProgressOrders != null && inProgressOrders.Any())
            {
                var ordersList = string.Join("\n  ", inProgressOrders.Select(o => $"• {o.CarModel} ({o.CarNumber}) - {o.Time:HH:mm}"));
                return $"Нельзя закрыть смену! Есть заказы со статусом \"Выполняется\":\n  {ordersList}\n\nЗавершите или отмените эти заказы.";
            }

            // Проверяем записи со статусом "Предварительная запись" на сегодня
            var todayAppointments = _data.Appointments
                .Where(a => a.AppointmentDate.Date == DateTime.Now.Date && !a.IsCompleted)
                .ToList();

            if (todayAppointments.Any())
            {
                var appointmentsList = string.Join("\n  ", todayAppointments.Select(a => $"• {a.CarModel} ({a.CarNumber}) - {a.AppointmentDate:HH:mm}"));
                return $"Нельзя закрыть смену! Есть активные предварительные записи на сегодня:\n  {appointmentsList}\n\nПреобразуйте записи в заказы или перенесите их.";
            }

            return null; // Можно закрывать
        }
        public void UpdateClientLoyalty(int clientId)
        {
            var appData = FileDataService.LoadData();
            var client = appData.Clients.FirstOrDefault(c => c.Id == clientId);
            if (client == null) return;

            var completedOrders = appData.Shifts
                .SelectMany(s => s.Orders ?? new List<CarWashOrder>())
                .Where(o => o.ClientId == clientId && o.Status == "Выполнен")
                .ToList();

            client.VisitsCount = completedOrders.Count;
            client.TotalSpent = completedOrders.Sum(o => o.FinalPrice);
            client.LastVisitDate = completedOrders.Any() ? completedOrders.Max(o => o.Time) : (DateTime?)null;

            FileDataService.SaveData(appData);
        }
        /// <summary>
        /// Рассчитывает заработок мойщика за смену с гарантией минимума 1000₽
        /// </summary>
        /// <param name="washerId">ID мойщика</param>
        /// <param name="orders">Заказы смены</param>
        /// <returns>Итоговая ЗП (не менее 1000₽)</returns>
        public decimal CalculateWasherEarningsWithMinimum(int washerId, List<CarWashOrder> orders)
        {
            // Считаем базовый заработок: 35% от суммы услуг (без скидок)
            decimal baseEarnings = orders
                .Where(o => o.WasherId == washerId && o.Status == "Выполнен")
                .Sum(o => {
                    // Пересчитываем сумму услуг БЕЗ скидки
                    var servicesTotal = (o.ServiceIds ?? new List<int>()).Sum(sid => {
                        var svc = _data.Services.FirstOrDefault(s => s.Id == sid);
                        return svc?.GetPrice(o.BodyTypeCategory) ?? 0;
                    });
                    return servicesTotal * 0.35m;
                });

            // Гарантия минимума 1000₽
            return Math.Max(baseEarnings, 1000m);
        }
    }
}
