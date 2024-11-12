using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Photon.Plugin.ASA.Equipment;
using NINA.Photon.Plugin.ASA.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Photon.Plugin.ASA.SequenceItems
{
    [ExportMetadata("Name", "Power On Mount")]
    [ExportMetadata("Description", "Powers on the ASA motor")]
    [ExportMetadata("Icon", "PowerSVG")]
    [ExportMetadata("Category", "ASA Tools")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class PowerOn : SequenceItem
    {

        [ImportingConstructor]
        public PowerOn() : this(ASAPlugin.MountMediator, ASAPlugin.ASAOptions, ASAPlugin.Mount)
        {
        }

        public PowerOn(IMountMediator mountMediator, IASAOptions options, IMount mount)
        {
            this.mountMediator = mountMediator;
            this.mount = mount;
  
        }

        private PowerOn(PowerOn cloneMe) : this(cloneMe.mountMediator, cloneMe.options, cloneMe.mount)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            return new PowerOn(this) { };
        }

        private IMountMediator mountMediator;
        private IMount mount;
        private ASAOptions options;
        private IList<string> issues = new List<string>();

        public IList<string> Issues
        {
            get => issues;
            set
            {
                issues = value;
                RaisePropertyChanged();
            }
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
           
            
            if (!mount.PowerOn())
            {
                throw new Exception("Failed to power on the ASA mount");
            }
        }



        public override string ToString()
        {
            return $"Category: {Category}, Item: {nameof(PowerOn)}";
        }
    }
}
