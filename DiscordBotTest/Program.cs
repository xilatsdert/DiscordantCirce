using System;
using System.IO;
using System.Xml;
using System.Threading.Tasks;
using DSharpPlus;
using System.Text;

namespace DiscordantCirce
{
    /// <summary>
    /// This class implements the central bot logic.
    /// It loads an oauth.txt file containing the oauth key to establish a connect to a server.
    /// It also loads any xml file from the form directory to create an array of TFS to be used on a server.
    /// </summary>
    class Program
    {

        //The reader needed to access the XML files and the directory location.
        static XmlReader xmlReader; //This is an abstract class so we don't need to instantiate a new version of it.
        static string path = Directory.GetCurrentDirectory() + "\\forms\\";
        static string oauthPath = Directory.GetCurrentDirectory(); //using the local file location.
        static StreamReader oauthFile;
        static string oauth;

        public static Form[] tfArray;
        
        static void Main(string[] args)
        {
            try
            {
                tfArray = GetForms();
                if(tfArray.Length == 0)
                {
                    throw new Exception();
                }
            }

            catch
            {
                Console.Write("No forms were loaded. Check the forms directory for properly formatted .xml files.");
                Console.ReadLine();
                Environment.Exit(4);
            }

            Console.WriteLine("Loaded forms:");
            foreach (Form f in tfArray)
            {
                Console.WriteLine("\t" + f.suffix);
            }

            try
            {
                using (oauthFile = File.OpenText(oauthPath + "\\oauth.txt")) //open and then close the file.
                {
                    oauth = oauthFile.ReadLine();
                    if(oauth.Length == 0 || oauth == null)
                    {
                        throw new FileLoadException();
                    }
                }
            }
            
            //Catches for three pertinent cases on the oauth file.
            catch (FileNotFoundException) //Catch the missing OAUTH file 
            {
                Console.WriteLine("The OAUTH file could not be loaded to authenticate the bot. Please make sure you have an ouath.txt file. PATH = " + oauthPath);
                Console.ReadKey();
                Environment.Exit(1); //Throw a non-zero error code since we failed.
            }

            catch (FileLoadException)
            {
                Console.WriteLine("There is a problem with the oauth.txt file. Either it is empty, or the file is corrupt. Recreate the file.");
                Console.ReadKey();
                Environment.Exit(2);
            }

            catch (NullReferenceException)
            {
                Console.WriteLine("There is a problem with the oauth.txt file. Either it is empty, or the file is corrupt. Recreate the file.");
                Console.ReadKey();
                Environment.Exit(2);
            }

            Run().GetAwaiter().GetResult();
            
        }

        public static int FormCount()
        {
            return Directory.GetFiles(path).Length;
        }

        public static Form[] GetForms()
        {
            string description = null;
            string suffix = null;

            Form[] temp = new Form[FormCount()];
            Console.WriteLine("Expecting about " + FormCount() + " forms.");
            int i = 0;
            foreach (string file in Directory.GetFiles(Directory.GetCurrentDirectory() + "\\forms\\"))
            {
                Console.WriteLine("\tReading from: " + Directory.GetCurrentDirectory() + "\\forms\\" + file);
                xmlReader = XmlReader.Create(file);

                xmlReader.ReadToFollowing("description");
               //xmlReader.MoveToFirstAttribute();
                description = xmlReader.ReadElementContentAsString();

                xmlReader.ReadToFollowing("suffix");
                suffix = xmlReader.ReadElementContentAsString();

                if (description != null && suffix != null)
                {
                    temp[i] = new Form(description, suffix);
                    i++;
                }
                
            }

            return temp;
        }

        /// <summary>
        /// This method cleans up a user's nickname to remove the -[form] suffix from the name.
        /// </summary>
        /// <param name="discord">The discord client </param>
        /// <param name="e">The meessage event</param>
        /// <param name="reset">This tells us if the invoked command is for a change or just a resetting of a form</param>
        /// <returns></returns>
        public static async Task Cleanup(DiscordClient discord, MessageCreateEventArgs e, bool reset)
        {
            if (reset)
            {
                await e.Message.Respond("The bot extends a shower head  before dousing " + e.Message.Author.Mention + "!");
            }
            string name = e.Guild.GetMember(e.Author.ID).Result.Nickname;
            Console.WriteLine(name);
            if (name != null && name.Length > 0)
            {
                try
                {
                    string newName = name.Split('-')[0];
                    Console.WriteLine(newName);
                    await discord.ModifyMember(e.Guild.ID, e.Message.Author.ID, newName);
                }

                catch (NullReferenceException)
                {
                    Console.WriteLine("There was an issue where we couldn't properly split the user ID from the suffix.");
                }
            }

            else
            {
                await discord.ModifyMember(e.Guild.ID, e.Message.Author.ID, name);
            }
        }
        
        /// <summary>
        /// This method sends a message to a user listing all of the loaded forms by suffix
        /// </summary>
        /// <param name="discord"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        public static async Task ListForms(DiscordClient discord, MessageCreateEventArgs e)
        {
            StringBuilder loadedForms = new StringBuilder();

            await discord.CreateDM(e.Message.Author.ID).Result.SendMessage("The currently loaded messages are: \n");

            foreach(Form f in tfArray)
            {
                loadedForms.Append(f.suffix + "\n");
            }

            await discord.CreateDM(e.Author.ID).Result.SendMessage(loadedForms.ToString());
        }

        /// <summary>
        /// This method sends a message to a user contaning all the functionalities in the bot!
        /// </summary>
        /// <param name="discord">The currently connected discord client</param>
        /// <param name="e">The message event object</param>
        /// <returns></returns>
        public static async Task HelpMessage(DiscordClient discord, MessageCreateEventArgs e)
        {
            try
            {
                await discord.CreateDM(e.Author.ID).Result.SendMessage("Thank you! In a channel, type !cleanup to undo a change, type !zap for a transformation, and type !test to get the local computer time!");
            }

            catch(Exception whoops)
            {
                Console.WriteLine("A message send error occured - Could not send the Help message DM -> " + whoops);
            }
        }

        /// <summary>
        /// This method sets a new form for a discord user. Pay careful attention to the format that the form description uses. The symbol @user is used to replace
        /// the subject in the form description.
        /// </summary>
        /// <param name="discord">The discord client. This allows us to easily acces the server and the user we need to make the change to.</param>
        /// <param name="e">The messagecreateevent object that allows us to access the message, get the author and the contents of the mesage.</param>
        /// <param name="form_description">The new form description. @user is replaced with user nickname.</param>
        /// <param name="suffix">-[form] suffix to attach to a user. E.G., Tyr become Tyr-wolf</param>
        /// <returns></returns>
        public static async Task FormSetter(DiscordClient discord, MessageCreateEventArgs e, string form_description, string suffix)
        {
            try
            {
                await Cleanup(discord, e, false);

                //adjust user nickname. If there is no nickname, take current username and append the suffix, creating a nickname.
                //else, we do create the new nickname using an existing nickname. 
                if(e.Guild.GetMember(e.Message.Author.ID).Result.Nickname == null ||
                    e.Guild.GetMember(e.Message.Author.ID).Result.Nickname.Length == 0)
                {
                    string newDescriptor = form_description.Replace("@user", e.Message.Author.Username);
                    await e.Message.Respond(newDescriptor);
                    await discord.ModifyMember(e.Guild.ID, e.Message.Author.ID, e.Message.Author.Username + suffix);
                    
                }
                else
                {
                    await e.Message.Respond(form_description.Replace("@user", e.Guild.GetMember(e.Author.ID).Result.Nickname));
                    await discord.ModifyMember(e.Guild.ID, e.Message.Author.ID, e.Guild.GetMember(e.Author.ID).Result.Nickname + suffix);
                }

            }

            catch (Exception whoops) //This code catches everything. The log is left on the running platform so we can catch exceptions, 
            //And tells the end user to contact the author of the code.
            {
                await discord.CreateDM(e.Author.ID).Result.SendMessage("Something went really wrong! Contact Xilats!");
                Console.WriteLine("And exception was caught: " + whoops);
            }
        }

        public static async Task Run()
        {
            var discord = new DiscordClient(new DiscordConfig
            {
                AutoReconnect = true,
                DiscordBranch = Branch.Stable,
                LargeThreshold = 250,
                LogLevel = LogLevel.Unnecessary,
                Token = oauth,
                TokenType = TokenType.Bot,
                UseInternalLogHandler = false
             });

            discord.DebugLogger.LogMessageReceived += (o, e) =>
            {
                Console.WriteLine($"[{e.TimeStamp}] [{e.Application}] [{e.Level}] {e.Message}");
            };

            discord.GuildAvailable += e =>
            {
                discord.DebugLogger.LogMessage(LogLevel.Info, "discord bot", $"Guild available: {e.Guild.Name}", DateTime.Now);
                return Task.Delay(0);
            };

            discord.MessageCreated += async e =>
            {
                if (e.Message.Content.ToLower() == "!test")
                {
                    await e.Message.Respond("The time is " + DateTime.Now);
                }

                if(e.Message.Content.ToLower() == "!help")
                {
                    await HelpMessage(discord, e);
                }

                if(e.Message.Content.ToLower() == "!list")
                {
                    await ListForms(discord, e);
                }

                if(e.Message.Content.ToLower() == "!cleanup")
                {
                    await Cleanup(discord, e, true);
                }

                if (e.Message.Content.ToLower() == "!zap")
                {
                    await e.Message.Respond("The bot vibrates before blasting " + e.Message.Author.Mention +"!");

                    Random rand = new Random();
                    int form = rand.Next(tfArray.Length);

                    try
                    {
                        await FormSetter(discord, e, tfArray[form].description, tfArray[form].suffix);      
                        form = rand.Next(tfArray.Length);
                    }
                    
                    catch (Exception whoops)
                    {
                        Console.WriteLine("You fucked up: " + whoops);
                    }
                }
            };

            await discord.Connect();
            await Task.Delay(-1);
        }

    }
}
