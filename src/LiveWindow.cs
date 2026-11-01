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
            public int Id; public string Label; public string Reason;
            public override string ToString(){return Label;}
        }
        readonly ComboBox targets=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=690};
        readonly ComboBox themes=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=320};
        readonly CheckBox wallpaper=new CheckBox {Text="Картинка на крупных фонах Cairo (экспериментально)",AutoSize=true};
        readonly TrackBar darkness=new TrackBar {Minimum=0,Maximum=100,Value=65,Width=260,TickFrequency=10};
        readonly Label status=new Label {AutoSize=false,Width=740,Height=95,Text="Выбери тестовый Конфигуратор и нажми «Включить»."};
        readonly Label darkLabel=new Label {Text="Затемнение картинки: 65%",AutoSize=true};
        readonly Button enable,disable,refresh;
        readonly string log=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Dark1C","launcher.log");
        readonly string profilePath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Dark1C","profile.xml");
        string profileError;
        bool busy;
        public LiveWindow() {
            Text="Dark1C 0.2.1 · 1С 8.5.1.1529 x64 · предварительная версия";
            ClientSize=new Size(790,510);MinimumSize=new Size(800,550);
            StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(30,30,30);ForeColor=Color.Gainsboro;Font=new Font("Segoe UI",10);
            var flow=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(20),AutoScroll=true};
            flow.Controls.Add(new Label{Text="Тёмная тема для настоящего Конфигуратора",AutoSize=true,Font=new Font(Font,FontStyle.Bold),Margin=new Padding(0,0,0,12)});
            flow.Controls.Add(targets);
            flow.Controls.Add(new Label{Text="Оформление",AutoSize=true,Margin=new Padding(0,12,0,4)});
            themes.Items.AddRange(new object[]{"Графит — тёмная тема","Светлая — мягкие серые оттенки"});themes.SelectedIndex=0;flow.Controls.Add(themes);
            var actions=new FlowLayoutPanel{Width=740,Height=82,Margin=new Padding(0,8,0,8)};
            enable=Button("Включить");disable=Button("Выключить");refresh=Button("Обновить список");var preview=Button("Предпросмотр");
            var elevated=Button("От администратора");
            actions.Controls.AddRange(new Control[]{enable,disable,refresh,preview,elevated});flow.Controls.Add(actions);
            flow.Controls.Add(wallpaper);flow.Controls.Add(darkLabel);flow.Controls.Add(darkness);flow.Controls.Add(status);
            flow.Controls.Add(new Label{Text="OFF возвращает исходные вызовы рисования. DLL остаётся до закрытия 1С.\r\nПервая проверка — на копии базы. Охват интерфейса пока частичный.",AutoSize=true});Controls.Add(flow);
            refresh.Click+=delegate{RefreshTargets();};preview.Click+=delegate{new MainWindow().Show(this);};
            elevated.Click+=delegate{if(busy)return;try{Process.Start(new ProcessStartInfo(typeof(LiveWindow).Assembly.Location){UseShellExecute=true,Verb="runas"});Close();}catch(Exception ex){status.Text="Перезапуск отменён или недоступен: "+ex.Message;}};
            targets.SelectedIndexChanged+=delegate{var t=targets.SelectedItem as Target;if(t!=null){enable.Enabled=disable.Enabled=t.Reason==null;status.Text=t.Reason??"Сеанс прошёл проверку. Можно применить тему.";}};
            enable.Click+=async delegate{await Apply(true);};disable.Click+=async delegate{await Apply(false);};
            darkness.ValueChanged+=delegate{darkLabel.Text="Затемнение картинки: "+darkness.Value+"%";};
            try {var p=Profile.Load(profilePath);themes.SelectedIndex=p.Theme;wallpaper.Checked=p.Wallpaper;darkness.Value=p.Darkness;}
            catch(Exception ex){profileError="Профиль не загружен; использованы исходные настройки. "+ex.Message;WriteLog(profileError);}
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy){e.Cancel=true;return;}SaveProfile();};
            Shown+=delegate{RefreshTargets();if(profileError!=null)status.Text=profileError;};
        }
        Button Button(string label){return new Button{Text=label,AutoSize=true,Height=34,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(48,48,48),ForeColor=ForeColor,Margin=new Padding(0,0,10,0)};}
        void RefreshTargets() {
            var old=targets.SelectedItem as Target;targets.Items.Clear();int rejected=0;
            foreach(Process p in Process.GetProcessesByName("1cv8"))using(p){
                var t=new Target{Id=p.Id,Label="PID "+p.Id};
                try{t.Label+=" · "+p.MainWindowTitle;Native.Validate(p);}
                catch(Exception ex){t.Reason=ex.Message;t.Label+=" · недоступен/не поддерживается";rejected++;}
                targets.Items.Add(t);if(old!=null&&old.Id==t.Id)targets.SelectedItem=t;
            }
            if(targets.Items.Count==1)targets.SelectedIndex=0;
            enable.Enabled=disable.Enabled=targets.SelectedItem is Target&&((Target)targets.SelectedItem).Reason==null;
            var selected=targets.SelectedItem as Target;
            status.Text=selected!=null?(selected.Reason??"Сеанс прошёл проверку. Проверь PID перед подключением."):
                targets.Items.Count==0?"Процесс 1cv8.exe не найден. Открой Конфигуратор и нажми «Обновить список».":"Найдено процессов: "+targets.Items.Count+", недоступных: "+rejected+". Выбери сеанс для подробностей.";
        }
        async Task Apply(bool on) {
            var target=targets.SelectedItem as Target;if(target==null){status.Text="Сначала выбери Конфигуратор.";return;}
            if(target.Reason!=null){status.Text=target.Reason;return;}
            bool picture=wallpaper.Checked;int dark=darkness.Value;int theme=themes.SelectedIndex;
            busy=true;enable.Enabled=disable.Enabled=refresh.Enabled=targets.Enabled=themes.Enabled=wallpaper.Enabled=darkness.Enabled=false;status.Text="Ожидание ответа нативного модуля…";
            try {
                await Task.Run(delegate{using(Process p=Process.GetProcessById(target.Id)){Native.Validate(p);Native.Apply(p,on,picture,dark,theme);}});
                int[] s=Native.State(target.Id,null,null,null);
                status.Text=(on?"Тема включена: "+themes.SelectedItem:"Тема выключена")+" · PID "+target.Id+" · перехватов: "+(s==null?0:s[3])+". Если сохранилась старая заливка, переоткрой окно редактора.";
                WriteLog(status.Text);
                SaveProfile();
            }catch(Exception ex){status.Text=ex.Message;WriteLog(ex.ToString());}
            finally{busy=false;enable.Enabled=disable.Enabled=refresh.Enabled=targets.Enabled=themes.Enabled=wallpaper.Enabled=darkness.Enabled=true;}
        }
        void SaveProfile(){try{new Profile{Theme=themes.SelectedIndex,Wallpaper=wallpaper.Checked,Darkness=darkness.Value}.Save(profilePath);}catch(Exception ex){WriteLog(ex.ToString());status.Text="Настройки не сохранены: "+ex.Message;}}
        void WriteLog(string message){try{Directory.CreateDirectory(Path.GetDirectoryName(log));File.AppendAllText(log,DateTime.Now.ToString("s")+" "+message+Environment.NewLine,Encoding.UTF8);}catch{}}
        [STAThread]static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new LiveWindow());}
    }
}
