using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using DSharpPlus;

namespace DiscordantCirce
{
    /// <summary>
    /// This class implements a cooldown mechanism to keep users from abusing the TF function.
    /// By default, we set cooldown to 30 seconds.
    /// We use the built in Diagnostic, 
    /// </summary>
    public class CoolingUser
    {
        //This class consists of two members, a user who just called a tf, and a cooldown timer.
        public DiscordUser user;
        public Stopwatch cooldown;

        /// <summary>
        /// This constructor just needs a user to be added to a datastructure for the cooldown to start. We recommend a linked list in this case.
        /// </summary>
        /// <param name="user">The DiscordUser object that called the tf message</param>
        public CoolingUser(DiscordUser user)
        {
            this.user = user;
            this.cooldown = new Stopwatch();
            cooldownStart();
        }

        /// <summary>
        /// This method starts the cooldown process.
        /// </summary>
        /// <returns></returns>
        private void cooldownStart()
        {
            cooldown.Start();
            while (cooldown.IsRunning)
            {
                if (cooldown.ElapsedMilliseconds == 30000)
                {
                    cooldown.Stop();
                }
            }
        }
    }
}
