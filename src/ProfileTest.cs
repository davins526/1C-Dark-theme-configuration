using System;
using System.IO;
namespace Dark1C {
    static class ProfileTest {
        static int Main(string[] args) {
            string dir=Path.Combine(Path.GetTempPath(),"Dark1C-profile-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            string path=Path.Combine(dir,"profile.xml");
            try {
                if(Profile.Load(path).Darkness!=65)throw new Exception("Missing profile defaults");
                new Profile{Theme=1,Wallpaper=true,Darkness=42,CodeFont="JetBrains Mono",UiFont="Inter",CodeSize=14}.Save(path);
                var p=Profile.Load(path);if(p.Theme!=1||!p.Wallpaper||p.Darkness!=42)throw new Exception("Roundtrip failed");
                if(p.CodeFont!="JetBrains Mono"||p.UiFont!="Inter"||p.CodeSize!=14)throw new Exception("Font preferences lost");
                p.Theme=0;p.Save(path);if(Profile.Load(path).Theme!=0)throw new Exception("Atomic replacement failed");
                File.WriteAllText(path,"<Profile><Version>1</Version><Theme>9</Theme></Profile>");
                try{Profile.Load(path);throw new Exception("Invalid theme accepted");}catch(InvalidDataException){}
                File.WriteAllText(path,"<Profile><Version>1</Version><Theme>0</Theme></Profile>");
                if(Profile.Load(path).CodeFont!="Consolas")throw new Exception("Legacy profile compatibility failed");
                File.WriteAllText(path,"<Profile><CodeSize>0</CodeSize></Profile>");
                try{Profile.Load(path);throw new Exception("Invalid font size accepted");}catch(InvalidDataException){}
                File.WriteAllText(path,"<!DOCTYPE Profile [<!ENTITY x SYSTEM 'file:///nonexistent'>]><Profile>&x;</Profile>");
                bool refused=false;try{Profile.Load(path);}catch(InvalidOperationException){refused=true;}catch(System.Xml.XmlException){refused=true;}
                if(!refused)throw new Exception("DTD accepted");
                Console.WriteLine("PASS: profile defaults, save/load, atomic replace, invalid values, DTD rejection.");return 0;
            }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
            finally{Directory.Delete(dir,true);}
        }
    }
}
