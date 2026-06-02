using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AccuratSystem.Contracts.Models;
using AccuratPanelCWM.Services;
using System.Collections.ObjectModel;
using Microsoft.Maui.Storage;

namespace AccuratPanelCWM.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly ApiService _apiService;
        private User _authenticatedUser;

        [ObservableProperty] private string _login;
        [ObservableProperty] private string _password;
        [ObservableProperty] private bool _isBusy;

        // Состояния UI для двухшаговой авторизации
        [ObservableProperty] private bool _isCredentialsStep = true;
        [ObservableProperty] private bool _isBranchStep = false;

        [ObservableProperty] private Branch _selectedBranch;
        public ObservableCollection<Branch> Branches { get; } = new();

        public LoginViewModel(ApiService apiService)
        {
            _apiService = apiService;
        }

        [RelayCommand]
        private async Task LoginAsync()
        {
            if (IsCredentialsStep)
            {
                await ProcessCredentialsStepAsync();
            }
            else if (IsBranchStep)
            {
                await ProcessBranchSelectionStepAsync();
            }
        }

        private async Task ProcessCredentialsStepAsync()
        {
            if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrWhiteSpace(Password))
            {
                await Application.Current.MainPage.DisplayAlert("Внимание", "Введите логин и пароль!", "ОК");
                return;
            }

            IsBusy = true;
            try
            {
                // Сносим старые данные сессии ДО авторизации
                Preferences.Default.Remove("CompanyId");
                Preferences.Default.Remove("CurrentBranchId");
                Preferences.Default.Remove("CurrentBranchName");

                // 1. Авторизуемся и получаем ПОЛНЫЙ ответ (как в WPF)
                var loginResponse = await _apiService.AuthenticateAsync(Login.Trim(), Password);

                if (loginResponse != null && loginResponse.User != null)
                {
                    _authenticatedUser = loginResponse.User;
                    Preferences.Default.Set("CompanyId", _authenticatedUser.CompanyId ?? 0);

                    // 2. БЕРЕМ ФИЛИАЛЫ ПРЯМО ОТ СЕРВЕРА! Никаких GetBranchesAsync()
                    var branches = loginResponse.AvailableBranches;

                    Branches.Clear();
                    if (branches != null)
                    {
                        foreach (var b in branches) Branches.Add(b);
                    }

                    // 3. АНАЛИЗИРУЕМ СИТУАЦИЮ
                    if (Branches.Count == 0)
                    {
                        await Application.Current.MainPage.DisplayAlert("Доступ запрещен", "У вас нет доступных филиалов.", "ОК");
                        return;
                    }
                    else if (Branches.Count == 1)
                    {
                        // Мойщик: филиал один -> пускаем бесшовно
                        SelectedBranch = Branches.First();
                        await ProcessBranchSelectionStepAsync();
                    }
                    else
                    {
                        // Директор: филиалов много -> переключаем UI на выбор филиала
                        SelectedBranch = Branches.First();
                        IsCredentialsStep = false;
                        IsBranchStep = true;
                    }
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка авторизации", ex.Message, "ОК");
            }
            finally { IsBusy = false; }
        }

        private async Task ProcessBranchSelectionStepAsync()
        {
            if (SelectedBranch == null) return;

            IsBusy = true;

            // Сохраняем выбранную рабочую точку
            Preferences.Default.Set("CurrentBranchId", SelectedBranch.Id);
            Preferences.Default.Set("CurrentBranchName", SelectedBranch.Name);
            Preferences.Default.Set("CurrentBranchWashBaysCount", SelectedBranch.WashBaysCount);
            Preferences.Default.Set("CurrentBranchServiceLiftsCount", SelectedBranch.ServiceLiftsCount);

            // Сохраняем данные для автовхода (по желанию)
            Preferences.Default.Set("SavedLogin", Login.Trim());
            Preferences.Default.Set("SavedPassword", Password); // На проде лучше юзать SecureStorage

            IsBusy = false;

            // Запускаем приложение!
            Application.Current.MainPage = new AppShell();
        }

        [RelayCommand]
        private void CancelBranchSelection()
        {
            // Кнопка "Назад", если передумал выбирать филиал
            IsBranchStep = false;
            IsCredentialsStep = true;
            _authenticatedUser = null;
        }
    }
}