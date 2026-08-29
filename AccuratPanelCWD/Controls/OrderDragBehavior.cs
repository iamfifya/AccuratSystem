using System.Windows;
using System.Windows.Input;

namespace AccuratPanelCWD.Controls
{
    /// <summary>
    /// Поведение для drag-and-drop карточек заказов.
    /// Восстанавливает логику из старого MainWindow.xaml.cs:
    /// - Запрещает перетаскивание выполненных заказов
    /// - Использует DataObject с форматом "OrderItem"
    /// </summary>
    public static class OrderDragBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled", typeof(bool), typeof(OrderDragBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                if ((bool)e.NewValue) element.MouseMove += OnMouseMove;
                else element.MouseMove -= OnMouseMove;
            }
        }

        private static void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            if (sender is FrameworkElement fe && fe.DataContext != null)
            {
                var order = fe.DataContext;

                // Проверяем IsCompleted через reflection (так как тип OrderDisplayItem может быть в другой сборке)
                var isCompletedProp = order.GetType().GetProperty("IsCompleted");
                if (isCompletedProp != null)
                {
                    var isCompleted = (bool?)isCompletedProp.GetValue(order);
                    if (isCompleted == true)
                    {
                        // Заказ выполнен — не начинаем перетаскивание
                        return;
                    }
                }

                // Создаем DataObject с форматом "OrderItem" для совместимости с Order_Drop
                var data = new DataObject("OrderItem", order);
                DragDrop.DoDragDrop(fe, data, DragDropEffects.Move);
            }
        }
    }
}