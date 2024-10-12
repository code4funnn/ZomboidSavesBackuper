using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZomboidSavesBackuper.Data
{
    public class ExistedSave
    {
        private readonly string _locationFolder;
        private readonly string _saveFolder;

        public string LocationFolder => _locationFolder;

        public string SaveFolder => _saveFolder;

        public ExistedSave(string locationFolder, string saveFolder)
        {
            _locationFolder = locationFolder;
            _saveFolder = saveFolder;
        }
    }
}
