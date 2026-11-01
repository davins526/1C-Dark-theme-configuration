using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Dark1C {
    public sealed class Profile {
        public int Version=1;
        public int Theme;
        public bool Wallpaper;
        public int Darkness=65;
        // Preview-only until font metrics are validated against the real editor.
        public string CodeFont="Consolas";
        public string UiFont="Segoe UI";
        public int CodeSize=12;
        internal void Validate() {
            if(Version!=1||Theme<0||Theme>1||Darkness<0||Darkness>100)
                throw new InvalidDataException("Некорректный профиль оформления.");
            if(string.IsNullOrWhiteSpace(CodeFont)||CodeFont.Length>64||string.IsNullOrWhiteSpace(UiFont)||UiFont.Length>64||CodeSize<9||CodeSize>24)
                throw new InvalidDataException("Некорректные параметры шрифта.");
        }
        internal static Profile Load(string path) {
            if(!File.Exists(path))return new Profile();
            var settings=new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16384};
            using(var reader=XmlReader.Create(path,settings)) {
                var profile=(Profile)new XmlSerializer(typeof(Profile)).Deserialize(reader);
                profile.Validate();return profile;
            }
        }
        internal void Save(string path) {
            Validate();Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                using(var writer=new StreamWriter(temporary,false,new System.Text.UTF8Encoding(false)))
                    new XmlSerializer(typeof(Profile)).Serialize(writer,this);
                if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
            }finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
}
