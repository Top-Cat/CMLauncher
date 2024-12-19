using System;
using System.IO;
using AppKit;
using Foundation;

namespace OSX
{
    public partial class ViewController : NSViewController
    {
        public ViewController(IntPtr handle) : base(handle)
        {
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            // Do any additional setup after loading the view.

            progressLabel.StringValue = "Checking for updates";
            progressBar.Indeterminate = true;

            IVersion ver = Version.GetVersion();
            var macSpecific = new MacSpecific(progressLabel, progressBar);
            if (MainClass.CleanDownload || !File.Exists($"{macSpecific.GetDownloadFolder()}/{macSpecific.GetBinaryPath()}"))
            {
                ver.Update(0, "");
            }

            new Main(macSpecific);
            
            // \o/
        }

        public override void ViewWillAppear()
        {
            base.ViewWillAppear();

            var window = View.Window;
            window.Center();
        }

        public override NSObject RepresentedObject
        {
            get
            {
                return base.RepresentedObject;
            }
            set
            {
                base.RepresentedObject = value;
            }
        }
    }
}
