using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using OfficeOpenXml;
using VerificationAirVelocitySensor.Model;
using VerificationAirVelocitySensor.ViewModel.BaseVm;
using VerificationAirVelocitySensor.ViewModel.Services;
using YamlDotNet.Serialization;

namespace VerificationAirVelocitySensor.ViewModel
{
    public class MainWindowVm : BaseVm.BaseVm
    {
        #region RealyCommand

        #region Частотомер ЧЗ-85/6

        #region Set Gate Time

        public RelayCommand SetGateTime1SCommand =>
            new RelayCommand(() => SetGateTime(GateTime.S1), SetGateTime1SValidation);

        public RelayCommand SetGateTime4SCommand =>
            new RelayCommand(() => SetGateTime(GateTime.S4), SetGateTime4SValidation);

        public RelayCommand SetGateTime7SCommand =>
            new RelayCommand(() => SetGateTime(GateTime.S7), SetGateTime7SValidation);

        public RelayCommand SetGateTime10SCommand =>
            new RelayCommand(() => SetGateTime(GateTime.S10), SetGateTime10SValidation);

        public RelayCommand SetGateTime100SCommand =>
            new RelayCommand(() => SetGateTime(GateTime.S100), SetGateTime100SValidation);

        #endregion

        public RelayCommand ReadValueOnFrequencyCounterCommand =>
            new RelayCommand(ReadValueOnFrequencyCounter, FrequencyCounterDevice.Instance.IsOpen);

        public RelayCommand ResetCommand => new RelayCommand(FrequencyCounterDevice.Instance.RstCommand,
            FrequencyCounterDevice.Instance.IsOpen);

        public RelayCommand SetFrequencyChannel1Command =>
            new RelayCommand(() => SetFrequencyChannel(FrequencyChannel.Channel1), SetFrequencyChannel1Validation);

        public RelayCommand SetFrequencyChannel2Command =>
            new RelayCommand(() => SetFrequencyChannel(FrequencyChannel.Channel2), SetFrequencyChannel2Validation);

        public RelayCommand OnFilterChannel1Command =>
            new RelayCommand(() => OnOffFilter(1, true), OnFilterChannel1Validation);

        public RelayCommand OffFilterChannel1Command =>
            new RelayCommand(() => OnOffFilter(1, false), OffFilterChannel1Validation);

        public RelayCommand OnFilterChannel2Command =>
            new RelayCommand(() => OnOffFilter(2, true), OnFilterChannel2Validation);

        public RelayCommand OffFilterChannel2Command =>
            new RelayCommand(() => OnOffFilter(2, false), OffFilterChannel2Validation);

        public RelayCommand OpenPortFrequencyCounterCommand => new RelayCommand(OpenPortFrequencyCounterDevice);

        public RelayCommand ClosePortFrequencyCounterCommand =>
            new RelayCommand(() => FrequencyCounterDevice.Instance.ClosePort(), FrequencyCounterDevice.Instance.IsOpen);

        #endregion

        #region Анемометр / Частотный двигатель

        public RelayCommand OpenPortFrequencyMotorCommand =>
            new RelayCommand(() => FrequencyMotorDevice.Instance.OpenPort(ComPortFrequencyMotor));

        public RelayCommand ClosePortFrequencyMotorCommand =>
            new RelayCommand(() => FrequencyMotorDevice.Instance.ClosePort(), FrequencyMotorDevice.Instance.IsOpen);

        public RelayCommand StopFrequencyMotorCommand =>
            new RelayCommand(() => FrequencyMotorDevice.Instance.SetFrequency(0, 0),
                FrequencyMotorDevice.Instance.IsOpen);

        public RelayCommand SetSpeedFrequencyMotorCommand => new RelayCommand(SetSpeedFrequencyMotorMethodAsync,
            FrequencyMotorDevice.Instance.IsOpen);

        #endregion

        public RelayCommand OpenCloseConnectionMenuCommand => new RelayCommand(OpenCloseConnectionMenu);

        public RelayCommand OpenCloseDebuggingMenuCommand =>
            new RelayCommand(OpenCloseDebuggingMenu, OpenCloseDebuggingMenuValidation);

        public RelayCommand UpdateComPortsSourceCommand => new RelayCommand(UpdateComPortsSource);
        public RelayCommand OpenReferenceCommand => new RelayCommand(() => IsReference = !IsReference);

        public RelayCommand StartTestCommand => new RelayCommand(StartTest);

        public RelayCommand OpenCloseSetSpeedPointsMenuCommand => new RelayCommand(OpenCloseSetSpeedPoints);

        #endregion

        #region Property

        private UserSettings _userSettings;
        private readonly object _loker = new object();

        private const string PathUserSettings = "UserSettings.txt";

        /// <summary>
        /// Флаг для отображения справки :)
        /// </summary>
        public bool IsReference { get; set; }

        public decimal FrequencyCounterValue { get; set; }
        public bool VisibilityConnectionMenu { get; set; }
        public bool VisibilityDebuggingMenu { get; set; }
        public bool VisibilitySetSpeedPoints { get; set; }

        public ObservableCollection<string> PortsList { get; set; } =
            new ObservableCollection<string>(SerialPort.GetPortNames());

        public bool FrequencyCounterIsOpen { get; set; }
        public bool FrequencyMotorIsOpen { get; set; }

        /// <summary>
        /// Параметр для установки частотв трубы.
        /// </summary>
        public int SetFrequencyMotor { get; set; }

        /// <summary>
        /// Эталонное значение скорости с частотной трубы
        /// </summary>
        public decimal SpeedReferenceValue { get; set; }

        public ObservableCollection<decimal> AverageSpeedReferenceCollection { get; set; } =
            new ObservableCollection<decimal>();

        private decimal _averageSpeedReferenceValue;

        private void UpdateAverageSpeedReferenceValue(decimal newValue)
        {
            if (AverageSpeedReferenceCollection.Count > 5 && _acceptCorrectionReference == false)
            {
                AverageSpeedReferenceCollection.Clear();
            }

            AverageSpeedReferenceCollection.Add(newValue);
            _averageSpeedReferenceValue = Math.Round(AverageSpeedReferenceCollection.Average(), 2);
        }

        /// <summary>
        /// Флаг для включения в колекцию среднего значения эталона , 
        /// всех его значений за время теста скоростной точки после прохождения корректировки.
        /// </summary>
        private bool _acceptCorrectionReference;

        /// <summary>
        /// Время ожидания после установки значения частоты, что бы дать аэротрубе стабилизировать значение
        /// </summary>
        public int WaitSetFrequency { get; set; } = 5000;

        //Все свойства что ниже, должны сохранятся пре перезапуске.
        //TODO Позже сделать переключатель на интерфейсе.
        public TypeTest _typeTest = TypeTest.Dvs02;

        public TypeTest TypeTest
        {
            get => _typeTest;
            set
            {
                _typeTest = value;
                OnPropertyChanged(nameof(TypeTest));
                _userSettings.TypeTest = value;
                Serialization();
            }
        }

        private bool _filterChannel1;

        public bool FilterChannel1
        {
            get => _filterChannel1;
            set
            {
                _filterChannel1 = value;
                OnPropertyChanged(nameof(FilterChannel1));
                _userSettings.FilterChannel1 = value;
                Serialization();
            }
        }

        private bool _filterChannel2;

        public bool FilterChannel2
        {
            get => _filterChannel2;
            set
            {
                _filterChannel2 = value;
                OnPropertyChanged(nameof(FilterChannel2));
                _userSettings.FilterChannel2 = value;
                Serialization();
            }
        }

        private FrequencyChannel _frequencyChannel;

        public FrequencyChannel FrequencyChannel
        {
            get => _frequencyChannel;
            set
            {
                _frequencyChannel = value;
                OnPropertyChanged(nameof(FrequencyChannel));
                _userSettings.FrequencyChannel = value;
                Serialization();
            }
        }

        private GateTime _gateTime;

        public GateTime GateTime
        {
            get => _gateTime;
            set
            {
                _gateTime = value;
                OnPropertyChanged(nameof(GateTime));
                _userSettings.GateTime = value;
                Serialization();
            }
        }

        private string _comPortFrequencyMotor;

        public string ComPortFrequencyMotor
        {
            get => _comPortFrequencyMotor;
            set
            {
                _comPortFrequencyMotor = value;
                OnPropertyChanged(nameof(ComPortFrequencyMotor));
                _userSettings.ComPortFrequencyMotor = value;
                Serialization();
            }
        }

        private string _comPortFrequencyCounter;

        public string ComPortFrequencyCounter
        {
            get => _comPortFrequencyCounter;
            set
            {
                _comPortFrequencyCounter = value;
                OnPropertyChanged(nameof(ComPortFrequencyCounter));
                _userSettings.ComPortFrequencyCounter = value;
                Serialization();
            }
        }

        #endregion

        #region RelayCommand Method

        private void OpenPortFrequencyCounterDevice()
        {
            FrequencyCounterDevice.Instance.OpenPort(ComPortFrequencyCounter);

            //TODO Что бы не выглядело как зависание, добавить BusyIndicator

            FrequencyCounterDevice.Instance.SetChannelFrequency(_userSettings.FrequencyChannel);
            FrequencyCounterDevice.Instance.SetGateTime(_userSettings.GateTime);
            FrequencyCounterDevice.Instance.SwitchFilter(1, _userSettings.FilterChannel1);
            FrequencyCounterDevice.Instance.SwitchFilter(2, _userSettings.FilterChannel2);
        }

        private static bool ValidationIsOpenPorts()
        {
            if (!FrequencyMotorDevice.Instance.IsOpen())
            {
                MessageBox.Show("Порт частотного двигателя закрыт", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            if (!FrequencyCounterDevice.Instance.IsOpen())
            {
                MessageBox.Show("Порт частотомера закрыт", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private void UpdateComPortsSource()
        {
            var newPortList = new ObservableCollection<string>(SerialPort.GetPortNames());

            //Добавляю новые итемы из полученной коллекции.
            foreach (var port in newPortList)
            {
                if (!PortsList.Contains(port))
                {
                    PortsList.Add(port);
                }
            }


            var deletePorts = new ObservableCollection<string>();

            //Записываю старые итемы в коллекцию на удаление
            foreach (var port in PortsList)
            {
                if (!newPortList.Contains(port))
                {
                    deletePorts.Add(port);
                }
            }

            //Удаляю лишние элементы
            foreach (var port in deletePorts)
            {
                PortsList.Remove(port);
            }
        }

        private void OpenCloseConnectionMenu() => VisibilityConnectionMenu = !VisibilityConnectionMenu;
        private void OpenCloseDebuggingMenu() => VisibilityDebuggingMenu = !VisibilityDebuggingMenu;
        private void OpenCloseSetSpeedPoints() => VisibilitySetSpeedPoints = !VisibilitySetSpeedPoints;

        private bool OpenCloseDebuggingMenuValidation() => FrequencyMotorIsOpen;

        #region Частотомер ЧЗ-85/6

        private void ReadValueOnFrequencyCounter()
        {
            FrequencyCounterValue = FrequencyCounterDevice.Instance.GetCurrentHzValue();
        }

        private void SetGateTime(GateTime gateTime)
        {
            //TODO Команда частотомера. Делать ли асинк ? Протестировать
            FrequencyCounterDevice.Instance.SetGateTime(gateTime);
            GateTime = gateTime;
        }

        private bool SetGateTime1SValidation() =>
            GateTime != GateTime.S1 && FrequencyCounterDevice.Instance.IsOpen();

        private bool SetGateTime4SValidation() =>
            GateTime != GateTime.S4 && FrequencyCounterDevice.Instance.IsOpen();

        private bool SetGateTime7SValidation() =>
            GateTime != GateTime.S7 && FrequencyCounterDevice.Instance.IsOpen();

        private bool SetGateTime10SValidation() =>
            GateTime != GateTime.S10 && FrequencyCounterDevice.Instance.IsOpen();

        private bool SetGateTime100SValidation() =>
            GateTime != GateTime.S100 && FrequencyCounterDevice.Instance.IsOpen();

        private void SetFrequencyChannel(FrequencyChannel frequencyChannel)
        {
            //TODO Команда частотомера. Делать ли асинк ? Протестировать
            FrequencyCounterDevice.Instance.SetChannelFrequency(frequencyChannel);
            FrequencyChannel = frequencyChannel;
        }

        private bool SetFrequencyChannel1Validation() =>
            FrequencyChannel != FrequencyChannel.Channel1 && FrequencyCounterDevice.Instance.IsOpen();

        private bool SetFrequencyChannel2Validation() =>
            FrequencyChannel != FrequencyChannel.Channel2 && FrequencyCounterDevice.Instance.IsOpen();

        private void OnOffFilter(int channel, bool isOn)
        {
            //TODO Команда частотомера. Делать ли асинк ? Протестировать
            switch (channel)
            {
                case 1:
                    FrequencyCounterDevice.Instance.SwitchFilter(1, isOn);
                    FilterChannel1 = isOn;
                    break;
                case 2:
                    FrequencyCounterDevice.Instance.SwitchFilter(2, isOn);
                    FilterChannel2 = isOn;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool OnFilterChannel1Validation() =>
            FrequencyCounterDevice.Instance.IsOpen() && !FilterChannel1;

        private bool OffFilterChannel1Validation() =>
            FrequencyCounterDevice.Instance.IsOpen() && FilterChannel1;

        private bool OnFilterChannel2Validation() =>
            FrequencyCounterDevice.Instance.IsOpen() && !FilterChannel2;

        private bool OffFilterChannel2Validation() =>
            FrequencyCounterDevice.Instance.IsOpen() && FilterChannel2;

        #endregion

        private void SetSpeedFrequencyMotorMethodAsync()
        {
            Task.Run(async () =>
                await Task.Run(() => FrequencyMotorDevice.Instance.SetFrequency(SetFrequencyMotor, 0)));
        }

        #endregion


        /// <summary>
        /// Значения скорости на которых нужно считать значения датчика.
        /// </summary>
        public ObservableCollection<ControlPointSpeedToFrequency> ControlPointSpeed { get; set; } =
            new ObservableCollection<ControlPointSpeedToFrequency>
            {
                new ControlPointSpeedToFrequency(0, 0.7m, 445),
                new ControlPointSpeedToFrequency(1, 5, 2605),
                new ControlPointSpeedToFrequency(2, 10, 5650),
                new ControlPointSpeedToFrequency(3, 15, 7750),
                new ControlPointSpeedToFrequency(4, 20, 10600),
                new ControlPointSpeedToFrequency(5, 25, 13600),
                new ControlPointSpeedToFrequency(6, 30, 16384)
            };

        #region Test Method

        public ObservableCollection<DvsValue> CollectionDvsValue { get; set; }
            = new ObservableCollection<DvsValue>();

        private void StartTest()
        {
            var isValidation = ValidationIsOpenPorts();

            if (isValidation == false) return;

            Task.Run(async () => await Task.Run(() =>
            {
                switch (TypeTest)
                {
                    case TypeTest.Dvs01:

                        break;
                    case TypeTest.Dvs02:
                        LoadDefaultValueCollectionDvs2Value();
                        try
                        {
                            StartTestDvs02(GateTime);
                        }
                        catch (Exception e)
                        {
                            //TODO log
                            Console.WriteLine(e.Message);
                        }
                        finally
                        {
                            FrequencyMotorDevice.Instance.SetFrequency(0, 0);

                            ResultToXlsxDvs2();
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }));
        }

        /// <summary>
        /// Метод для очистки от старых значений  CollectionDvsValue и заполнением пустых значений. Для ДВС2
        /// </summary>
        public void LoadDefaultValueCollectionDvs2Value()
        {
            Application.Current.Dispatcher?.Invoke(() =>
            {
                CollectionDvsValue?.Clear();

                CollectionDvsValue = new ObservableCollection<DvsValue>();

                foreach (var point in ControlPointSpeed)
                    CollectionDvsValue.Add(new DvsValue(point.Speed));
            });
        }


        public void StartTestDvs02(GateTime gateTime)
        {
            var timeOutCounter = FrequencyCounterDevice.Instance.GateTimeToMSec(gateTime) + 1000;

            foreach (var point in ControlPointSpeed)
            {
                _acceptCorrectionReference = false;

                FrequencyMotorDevice.Instance.SetFrequency(point.SetFrequency, point.Speed);

                //Время ожидания для стабилизации трубы
                Thread.Sleep(WaitSetFrequency);

                Application.Current.Dispatcher?.Invoke(AverageSpeedReferenceCollection.Clear);

                if (point.Speed == 30)
                    Thread.Sleep(10000);

                //Для скоростной точки 30, отключаю коррекцию скорости, так как труба не может разогнаться до 30 м/с . 
                //А где-то до 27-29 м/с
                if (point.Speed != 30)
                    FrequencyMotorDevice.Instance.CorrectionSpeedMotor(ref _averageSpeedReferenceValue);


                _acceptCorrectionReference = true;

                Thread.Sleep(250);

                CollectionDvsValue[point.Id].DeviceSpeedValue1 = FrequencyCounterDevice.Instance.GetCurrentHzValue();
                Thread.Sleep(timeOutCounter);
                CollectionDvsValue[point.Id].DeviceSpeedValue2 = FrequencyCounterDevice.Instance.GetCurrentHzValue();
                Thread.Sleep(timeOutCounter);
                CollectionDvsValue[point.Id].DeviceSpeedValue3 = FrequencyCounterDevice.Instance.GetCurrentHzValue();
                Thread.Sleep(timeOutCounter);
                CollectionDvsValue[point.Id].DeviceSpeedValue4 = FrequencyCounterDevice.Instance.GetCurrentHzValue();
                Thread.Sleep(timeOutCounter);
                CollectionDvsValue[point.Id].DeviceSpeedValue5 = FrequencyCounterDevice.Instance.GetCurrentHzValue();
                Thread.Sleep(timeOutCounter);

                CollectionDvsValue[point.Id].ReferenceSpeedValue = _averageSpeedReferenceValue;
            }
        }

        private void ResultToXlsxDvs2()
        {
            var pathExampleXlsxFile = @"Resources\Dvs2.xlsx";
            while (true)
            {
                if (File.Exists(pathExampleXlsxFile))
                    break;

                var errorMessage =
                    "Отсутствует файл образец test_cs_protocol.xlsx  " +
                    "Пожалуйста поместите файл и повторите попытку(ОК). Или нажмите отмена для пропуска создания .xlsx";

                var mb = MessageBox.Show(errorMessage, "Ошибка", MessageBoxButton.OKCancel);

                //Если было нажато ОК
                if (mb == MessageBoxResult.OK)
                    continue;

                //Если была нажата отмена
                return;
            }

            using (var package = new ExcelPackage(new FileInfo(pathExampleXlsxFile)))
            {
                var ws = package.Workbook.Worksheets.First();

                #region Заполнение значений

                #region 0.7

                ws.Cells[12, 12].Value = ConvertResultForXlsx(CollectionDvsValue[0].ReferenceSpeedValue);
                ws.Cells[12, 13].Value = ConvertResultForXlsx(CollectionDvsValue[0].DeviceSpeedValue1);
                ws.Cells[12, 14].Value = ConvertResultForXlsx(CollectionDvsValue[0].DeviceSpeedValue2);
                ws.Cells[12, 15].Value = ConvertResultForXlsx(CollectionDvsValue[0].DeviceSpeedValue3);
                ws.Cells[12, 16].Value = ConvertResultForXlsx(CollectionDvsValue[0].DeviceSpeedValue4);
                ws.Cells[12, 17].Value = ConvertResultForXlsx(CollectionDvsValue[0].DeviceSpeedValue5);

                #endregion

                #region 5

                ws.Cells[13, 12].Value = ConvertResultForXlsx(CollectionDvsValue[1].ReferenceSpeedValue);
                ws.Cells[13, 13].Value = ConvertResultForXlsx(CollectionDvsValue[1].DeviceSpeedValue1);
                ws.Cells[13, 14].Value = ConvertResultForXlsx(CollectionDvsValue[1].DeviceSpeedValue2);
                ws.Cells[13, 15].Value = ConvertResultForXlsx(CollectionDvsValue[1].DeviceSpeedValue3);
                ws.Cells[13, 16].Value = ConvertResultForXlsx(CollectionDvsValue[1].DeviceSpeedValue4);
                ws.Cells[13, 17].Value = ConvertResultForXlsx(CollectionDvsValue[1].DeviceSpeedValue5);

                #endregion

                #region 10

                ws.Cells[14, 12].Value = ConvertResultForXlsx(CollectionDvsValue[2].ReferenceSpeedValue);
                ws.Cells[14, 13].Value = ConvertResultForXlsx(CollectionDvsValue[2].DeviceSpeedValue1);
                ws.Cells[14, 14].Value = ConvertResultForXlsx(CollectionDvsValue[2].DeviceSpeedValue2);
                ws.Cells[14, 15].Value = ConvertResultForXlsx(CollectionDvsValue[2].DeviceSpeedValue3);
                ws.Cells[14, 16].Value = ConvertResultForXlsx(CollectionDvsValue[2].DeviceSpeedValue4);
                ws.Cells[14, 17].Value = ConvertResultForXlsx(CollectionDvsValue[2].DeviceSpeedValue5);

                #endregion

                #region 15

                ws.Cells[15, 12].Value = ConvertResultForXlsx(CollectionDvsValue[3].ReferenceSpeedValue);
                ws.Cells[15, 13].Value = ConvertResultForXlsx(CollectionDvsValue[3].DeviceSpeedValue1);
                ws.Cells[15, 14].Value = ConvertResultForXlsx(CollectionDvsValue[3].DeviceSpeedValue2);
                ws.Cells[15, 15].Value = ConvertResultForXlsx(CollectionDvsValue[3].DeviceSpeedValue3);
                ws.Cells[15, 16].Value = ConvertResultForXlsx(CollectionDvsValue[3].DeviceSpeedValue4);
                ws.Cells[15, 17].Value = ConvertResultForXlsx(CollectionDvsValue[3].DeviceSpeedValue5);

                #endregion

                #region 20

                ws.Cells[16, 12].Value = ConvertResultForXlsx(CollectionDvsValue[4].ReferenceSpeedValue);
                ws.Cells[16, 13].Value = ConvertResultForXlsx(CollectionDvsValue[4].DeviceSpeedValue1);
                ws.Cells[16, 14].Value = ConvertResultForXlsx(CollectionDvsValue[4].DeviceSpeedValue2);
                ws.Cells[16, 15].Value = ConvertResultForXlsx(CollectionDvsValue[4].DeviceSpeedValue3);
                ws.Cells[16, 16].Value = ConvertResultForXlsx(CollectionDvsValue[4].DeviceSpeedValue4);
                ws.Cells[16, 17].Value = ConvertResultForXlsx(CollectionDvsValue[4].DeviceSpeedValue5);

                #endregion

                #region 25

                ws.Cells[17, 12].Value = ConvertResultForXlsx(CollectionDvsValue[5].ReferenceSpeedValue);
                ws.Cells[17, 13].Value = ConvertResultForXlsx(CollectionDvsValue[5].DeviceSpeedValue1);
                ws.Cells[17, 14].Value = ConvertResultForXlsx(CollectionDvsValue[5].DeviceSpeedValue2);
                ws.Cells[17, 15].Value = ConvertResultForXlsx(CollectionDvsValue[5].DeviceSpeedValue3);
                ws.Cells[17, 16].Value = ConvertResultForXlsx(CollectionDvsValue[5].DeviceSpeedValue4);
                ws.Cells[17, 17].Value = ConvertResultForXlsx(CollectionDvsValue[5].DeviceSpeedValue5);

                #endregion

                #region 30

                ws.Cells[18, 12].Value = ConvertResultForXlsx(CollectionDvsValue[6].ReferenceSpeedValue);
                ws.Cells[18, 13].Value = ConvertResultForXlsx(CollectionDvsValue[6].DeviceSpeedValue1);
                ws.Cells[18, 14].Value = ConvertResultForXlsx(CollectionDvsValue[6].DeviceSpeedValue2);
                ws.Cells[18, 15].Value = ConvertResultForXlsx(CollectionDvsValue[6].DeviceSpeedValue3);
                ws.Cells[18, 16].Value = ConvertResultForXlsx(CollectionDvsValue[6].DeviceSpeedValue4);
                ws.Cells[18, 17].Value = ConvertResultForXlsx(CollectionDvsValue[6].DeviceSpeedValue5);

                #endregion

                #endregion

                //var xlsxCombinePath = Path.Combine(folderPath, $"{NameSc}.xlsx");

                package.SaveAs(new FileInfo($"{DateTime.Now:dd.MM.yyyy_HH-mm-ss}.xlsx"));
            }
        }

        private string ConvertResultForXlsx(decimal? value) =>
            value == null ? string.Empty : Convert.ToString(value);

        #endregion


        public MainWindowVm()
        {
            FrequencyCounterDevice.Instance.IsOpenUpdate += FrequencyCounter_IsOpenUpdate;
            FrequencyMotorDevice.Instance.IsOpenUpdate += FrequencyMotor_IsOpenUpdate;
            FrequencyMotorDevice.Instance.UpdateReferenceValue += FrequencyMotor_UpdateReferenceValue;
            FrequencyMotorDevice.Instance.UpdateSetFrequency += FrequencyMotor_UpdateSetFrequency;

            var deserialization = Deserialization();
            _userSettings = deserialization ?? new UserSettings();
            FilterChannel1 = _userSettings.FilterChannel1;
            FilterChannel2 = _userSettings.FilterChannel2;
            FrequencyChannel = _userSettings.FrequencyChannel;
            GateTime = _userSettings.GateTime;
            ComPortFrequencyMotor = _userSettings.ComPortFrequencyMotor;
            ComPortFrequencyCounter = _userSettings.ComPortFrequencyCounter;
        }


        #region EventHandler Method

        private void FrequencyMotor_UpdateSetFrequency(object sender, UpdateSetFrequencyEventArgs e)
        {
            SetFrequencyMotor = e.SetFrequency;
        }

        private void FrequencyMotor_UpdateReferenceValue(object sender, UpdateReferenceValueEventArgs e)
        {
            SpeedReferenceValue = (decimal) e.ReferenceValue;
            UpdateAverageSpeedReferenceValue(SpeedReferenceValue);
        }

        private void FrequencyMotor_IsOpenUpdate(object sender, IsOpenFrequencyMotorEventArgs e)
        {
            FrequencyMotorIsOpen = e.IsOpen;
        }

        private void FrequencyCounter_IsOpenUpdate(object sender, IsOpenFrequencyCounterEventArgs e)
        {
            FrequencyCounterIsOpen = e.IsOpen;
        }

        #endregion

        #region deserilize / serilize

        private void Serialization()
        {
            try
            {
                var serializer = new Serializer();

                lock (_loker)
                {
                    using (var file = File.Open(PathUserSettings, FileMode.Create))
                    {
                        using (var writer = new StreamWriter(file))
                        {
                            serializer.Serialize(writer, _userSettings);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private UserSettings Deserialization()
        {
            var deserializer = new Deserializer();

            using (var file = File.Open(PathUserSettings, FileMode.OpenOrCreate, FileAccess.Read))
            {
                using (var reader = new StreamReader(file))
                {
                    try
                    {
                        var userSettings = deserializer.Deserialize<UserSettings>(reader);

                        return userSettings;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
        }

        #endregion
    }

    public class ControlPointSpeedToFrequency
    {
        public ControlPointSpeedToFrequency(int id, decimal speed, int setFrequency)
        {
            Speed = speed;
            SetFrequency = setFrequency;
            Id = id;
        }

        public ControlPointSpeedToFrequency()
        {
        }

        public decimal Speed { get; set; }
        public int SetFrequency { get; set; }

        public int Id { get; set; }
    }

    public enum TypeTest
    {
        Dvs01 = 1,
        Dvs02 = 2
    }
}