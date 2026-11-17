using Smec.Models;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Windows.Input;
using Timer = System.Threading.Timer;

namespace Smec.ViewModels
{
    public partial class SysViewModel : ObservableObject
    {
        private readonly Stopwatch _watch = new();
        private readonly ManualResetEventSlim _pauseEvent = new(true); // true = не на паузе
        private CancellationTokenSource? _cts;
        private Timer? _uiTimer;

        private const int UiUpdatePeriodMs = 100; 
        private const int SimTickPeriodMs = 20;

        [DataMember]
        public bool HighlightLinks
        {
            get;
            set => SetProperty(ref field, value);
        }

        public bool IsSimulating
        {
            get;
            private set => SetProperty(ref field, value);
        }

        public bool IsPaused
        {
            get;
            private set => SetProperty(ref field, value);
        }

        public TimeSpan CurrentSimulationTime
        {
            get;
            private set => SetProperty(ref field, value);
        }

        // Флаг выбора режима работы цикла симуляции:
        // true  — асинхронный цикл (await Task.Delay + неблокирующая пауза, мгновенная остановка)
        // false — классический синхронный цикл (Thread.Sleep + ManualResetEventSlim)
        public bool UseAsyncSimulation
        {
            get;
            set => SetProperty(ref field, value);
        } = false;

        // Неблокирующая пауза для асинхронного режима
        private TaskCompletionSource<bool>? _asyncPauseTcs;

        private async Task SimulationLoopAsync(CancellationToken token)
        {
            StatusText = "Асинхронный цикл симуляции: Запущен...";
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // Асинхронное ожидание паузы (освобождает поток)
                    var pauseTask = _asyncPauseTcs?.Task;
                    if (pauseTask != null && !pauseTask.IsCompleted)
                    {
                        await pauseTask.WaitAsync(token);
                    }

                    SimulateTick();

                    // Асинхронная задержка такта (прерывается мгновенно при отмене)
                    if (SimTickPeriodMs > 0)
                    {
                        await Task.Delay(SimTickPeriodMs, token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Штатное завершение при нажатии Стоп
            }
            finally
            {
                StatusText = "Асинхронный цикл симуляции: Завершен.";
            }
        }


        private void SimulationLoop(CancellationToken token)
        {
            StatusText = "Синхронный цикл симуляции: Запущен...";
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // Если на паузе — поток уснет здесь без нагрузки на CPU.
                    // При завершении симуляции token выбросит OperationCanceledException и выйдет без зависания.
                    _pauseEvent.Wait(token);

                    SimulateTick();

                    Thread.Sleep(SimTickPeriodMs);
                }
            }
            catch (OperationCanceledException)
            {
                // Штатное завершение при нажатии Стоп
            }
            finally
            {
                StatusText = "Синхронный цикл симуляции: Завершен.";
            }
        }

        private Task? _simTask;

        public void StartSimulation()
        {
            if (CurrentModel == null || IsSimulating) return;

            IsSimulating = true;
            IsPaused = false;
            CurrentSimulationTime = TimeSpan.Zero;

            _pauseEvent.Set();
            _asyncPauseTcs = null;
            _cts = new CancellationTokenSource();

            RequestClearSelection?.Invoke();
            PreSimulate();

            _watch.Restart();
            _uiTimer = new Timer(_ => CurrentSimulationTime = _watch.Elapsed, null, 0, UiUpdatePeriodMs);

            var token = _cts.Token;

            // Выбираем вариант цикла по флагу
            _simTask = UseAsyncSimulation
                ? Task.Run(() => SimulationLoopAsync(token), token)
                : Task.Run(() => SimulationLoop(token), token);

            CommandManager.InvalidateRequerySuggested();
        }

        public void TogglePause(bool pause)
        {
            if (!IsSimulating || IsPaused == pause) return;

            IsPaused = pause;

            if (pause)
            {
                _watch.Stop();
                if (UseAsyncSimulation)
                    _asyncPauseTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                else
                    _pauseEvent.Reset();
            }
            else
            {
                _watch.Start();
                if (UseAsyncSimulation)
                    _asyncPauseTcs?.TrySetResult(true);
                else
                    _pauseEvent.Set();
            }
        }

        public async Task StopSimulationAsync()
        {
            if (!IsSimulating) return;

            // 1. Сигнализируем об отмене и будим оба механизма паузы
            _cts?.Cancel();
            _pauseEvent.Set();
            _asyncPauseTcs?.TrySetCanceled();

            // 2. Ждём завершения задачи
            if (_simTask != null)
            {
                try
                {
                    await _simTask;
                }
                catch (OperationCanceledException) { }
                finally
                {
                    _simTask = null;
                }
            }

            // 3. Очищаем ресурсы
            _uiTimer?.Dispose();
            _uiTimer = null;
            _watch.Stop();
            _cts?.Dispose();
            _cts = null;

            CurrentSimulationTime = _watch.Elapsed;
            PostSimulate();

            IsSimulating = false;
            IsPaused = false;

            CommandManager.InvalidateRequerySuggested();
        }



        public void PreSimulate()
        {
            var model = CurrentModel;
            if (model == null) return;

            // 1. Сначала узлы выставляют свои стартовые состояния
            if (model.Nodes != null)
            {
                foreach (var node in model.Nodes)
                {
                    node?.InitState();
                }
            }

            // 2. Затем линки связывают порты и сразу передают стартовые состояния на входы
            if (model.Links != null)
            {
                foreach (var link in model.Links)
                {
                    link?.AddLogicalConnection();
                }
            }
        }

        private Random _random = new();
        private void DoRandomStateChange()
        {
            var model = CurrentModel;
            int i = _random.Next(0, 2);

            var rnd = _random.NextDouble();
            switch (rnd)
            {
                case < 0.2:
                    model?.Nodes[0].Ports[i].State = PortState.Low;
                    break;
                case > 0.95:
                    model?.Nodes[0].Ports[i].State = PortState.Pos;
                    break;
          
            }

        }

        public void SimulateTick()
        {
            var model = CurrentModel; // Копируем ссылку в локальную переменную (потокобезопасно)
            if (model?.Nodes == null) return;

            // Вносим случайные изменения состояния портов (для демонстрации)
            //DoRandomStateChange();

            // проходимся по всем элементам и обновляем их состояние
            foreach (var item in model.Nodes)
            {
                item.UpdateState(_watch.Elapsed);
            }

        }

        public void PostSimulate()
        {
            var model = CurrentModel;
            if (model == null) return;

            // 1. Сначала разрываем логические связи (сбрасывает _isConnected, цвет и отписывает PropertyChanged)
            if (model.Links != null)
            {
                foreach (var link in model.Links)
                {
                    link?.RemoveLogicalConnection();
                }
            }

            // 2. Затем сбрасываем узлы и возвращаем все порты в Undefined
            if (model.Nodes != null)
            {
                foreach (var node in model.Nodes)
                {
                    if (node == null) continue;

                    // Сюда можно добавить node.ResetState()

                    if (node.Ports == null) continue;

                    foreach (var port in node.Ports)
                    {
                        if (port == null) continue;

                        port.RemoveAllInvocation();
                        port.State = PortState.Undefined;
                    }
                }
            }
        }


    }
}
