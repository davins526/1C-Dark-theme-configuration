using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Dark1C {
    sealed class LiveWindow : Form {
        sealed class Target {
            public int Id; public string Label;
            public override string ToString(){return Label;}
        }
        readonly ComboBox targets=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=690};
        readonly CheckBox wallpaper=new CheckBox {Text="Картинка на крупных фонах Cairo (экспериментально)",AutoSize=true};
        readonly TrackBar darkness=new TrackBar {Minimum=0,Maximum=100,Value=65,Width=260,TickFrequency=10};
        readonly Label status=new Label {AutoSize=false,Width=740,Height=95,Text="Выбери тестовый Конфигуратор и нажми «Включить»."};
        readonly Label darkLabel=new Label {Text="Затемнение картинки: 65%",AutoSize=true};
        readonly Button enable,disable,refresh;
        readonly string log=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Dark1C","launcher.log");
        bool busy;
        public LiveWindow() {
            Text="Dark1C · 1С 8.5.1.1529 x64 · тестовая версия";
            ClientSize=new Size(790,420);MinimumSize=new Size(800,460);
            StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(30,30,30);ForeColor=Color.Gainsboro;Font=new Font("Segoe UI",10);
            var flow=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(20),AutoScroll=true};
            flow.Controls.Add(new Label{Text="Тёмная тема для настоящего Конфигуратора",AutoSize=true,Font=new Font(Font,FontStyle.Bold),Margin=new Padding(0,0,0,12)});
            flow.Controls.Add(targets);
            var actions=new FlowLayoutPanel{Width=740,Height=48,Margin=new Padding(0,8,0,8)};
            enable=Button("Включить");disable=Button("Выключить");refresh=Button("Обновить список");var preview=Button("Предпросмотр");
            actions.Controls.AddRange(new Control[]{enable,disable,refresh,preview});flow.Controls.Add(actions);
            flow.Controls.Add(wallpaper);flow.Controls.Add(darkLabel);flow.Controls.Add(darkness);flow.Controls.Add(status);
            flow.Controls.Add(new Label{Text="OFF возвращает исходные вызовы рисования. DLL остаётся до закрытия 1С.\r\nПервая проверка — на копии базы. Охват интерфейса пока частичный.",AutoSize=true});Controls.Add(flow);
            refresh.Click+=delegate{RefreshTargets();};preview.Click+=delegate{new MainWindow().Show(this);};
            enable.Click+=async delegate{await Apply(true);};disable.Click+=async delegate{await Apply(false);};
            darkness.ValueChanged+=delegate{darkLabel.Text="Затемнение картинки: "+darkness.Value+"%";};
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy)e.Cancel=true;};
            Shown+=delegate{RefreshTargets();};
        }
        Button Button(string label){return new Button{Text=label,AutoSize=true,Height=34,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(48,48,48),ForeColor=ForeColor,Margin=new Padding(0,0,10,0)};}
        void RefreshTargets() {
            targets.Items.Clear();int rejected=0;
            foreach(Process p in Process.GetProcessesByName("1cv8"))using(p){try{Native.Validate(p);targets.Items.Add(new Target{Id=p.Id,Label="PID "+p.Id+" · "+p.MainWindowTitle});}catch{rejected++;}}
            if(targets.Items.Count==1)targets.SelectedIndex=0;
            status.Text=targets.Items.Count==0?"Подходящий Конфигуратор не найден. Нужен 1cv8.exe 8.5.1.1529 x64 с командой DESIGNER. Открой его и обнови список. Пропущено процессов: "+rejected:"Найдено Конфигураторов: "+targets.Items.Count+". Проверь PID перед подключением.";
        }
        async Task Apply(bool on) {
            var target=targets.SelectedItem as Target;if(target==null){status.Text="Сначала выбери Конфигуратор.";return;}
            bool picture=wallpaper.Checked;int dark=darkness.Value;busy=true;enable.Enabled=disable.Enabled=refresh.Enabled=targets.Enabled=false;status.Text="Ожидание ответа нативного модуля…";
            try {
                await Task.Run(delegate{using(Process p=Process.GetProcessById(target.Id)){Native.Validate(p);Native.Apply(p,on,picture,dark);}});
                int[] s=Native.State(target.Id,null,null,null);
                status.Text=(on?"Тема включена":"Тема выключена")+" · PID "+target.Id+" · перехватов: "+s[3]+". Если сохранилась старая заливка, переоткрой окно редактора.";
                WriteLog(status.Text);
            }catch(Exception ex){status.Text=ex.Message;WriteLog(ex.ToString());}
            finally{busy=false;enable.Enabled=disable.Enabled=refresh.Enabled=targets.Enabled=true;}
        }
        void WriteLog(string message){try{Directory.CreateDirectory(Path.GetDirectoryName(log));File.AppendAllText(log,DateTime.Now.ToString("s")+" "+message+Environment.NewLine,Encoding.UTF8);}catch{}}
        [STAThread]static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new LiveWindow());}
    }
}
