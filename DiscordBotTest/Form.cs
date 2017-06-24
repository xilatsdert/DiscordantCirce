using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
namespace DiscordantCirce
{
    /// <summary>
    /// This class implements the XML reader for pulling in forms from the folder containing XML documents describing TF forms.
    /// This will load into the array used for holding onto the forms in the main bot class.
    /// Think of this class as a wrapper for loading and describing forms.
    /// </summary>
    public class Form
    {
        //The two defining characteristics of the XML file, the description and the suffix.
        public string description;
        public string suffix;

        /// <summary>
        /// This method counts all the files in the forms XML directory.
        /// We use this to count how many forms we have. One form per XML file.
        /// </summary>
        /// <returns>The number of files we have that contain forms.</returns>

        public Form(string description, string suffix)
        {
            this.description = description;
            this.suffix = suffix;
        }

    }
}
