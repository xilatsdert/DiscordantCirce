using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Diagnostics;
using System.Threading;
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


        //data structures for handling the tfs - an array to prevent server-disk thrashing
        public static Form[] tfArray;

        //And one to prevent TF abuse. We will use a dictionary so we can easily store a cooling user (This is a poor man's dynamic database)
        //And another to actually track WHO is in the tank.
        //The ulong used in the dictionary and list below are the user ID's held by discord.
        public static Dictionary<ulong, CoolingUser> cooldowntank = new Dictionary<ulong, CoolingUser>();
        public static List<ulong> usercooldownIndex = new List<ulong>();
        public static bool cooldownLock = false; //the locker needed for thread keep alive.
        public static bool started = false;

        public static Thread cool;

        static void Main(string[] args)
        {
            try
            {
                tfArray = GetForms();
                if (tfArray.Length == 0)
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
                    if (oauth.Length == 0 || oauth == null)
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
                Environment.Exit(3);
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
        /// This method checks each item in the cooldown tank Dictionary for a user that has cooled down. Once they have, we have remove them from the dictionary and LIst tracker
        /// This needs to be tossed onto another thread or else we get a thread blocker.
        /// </summary>
        /// <returns>A new thread for process.</returns>
        /// 
        //TODO: Correct code to use a thread instead of a Task.
        public static void CoolDownCheck()
        {
            started = true;
            while (cooldownLock)
            {
                for (int i = 0; i < usercooldownIndex.Count; i++)
                {
                    if (cooldowntank[usercooldownIndex[i]].cooldown.ElapsedMilliseconds >= 10000)
                    {
                        ulong userID = usercooldownIndex[i];
                        cooldowntank.Remove(userID);
                        usercooldownIndex.Remove(userID);
                        Console.WriteLine("Removed a user.");
                    }
                }
            }

            /*await Task.WhenAll(
            Task.Run(() =>
            {
                for (int id = 0; id < usercooldownIndex.Count; id++)
                {
                    // while (cooldowntank.Count > 0)
                    {
                        if (cooldowntank[usercooldownIndex[id]].cooldown.ElapsedMilliseconds >= 10000)
                        {
                            ulong userId = usercooldownIndex[id];
                            cooldowntank.Remove(userId);
                            usercooldownIndex.Remove(userId);
                            Console.WriteLine("REMOVED A USER");
                        }

                        else
                        {
                            {
                                Console.WriteLine("User ID still in Timeout -> " + id + " :: Seconds in time out -> " + cooldowntank[usercooldownIndex[id]].cooldown.ElapsedMilliseconds / 1000);
                            }
                        }
                    }
                }
            })
            );*/
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
                await e.Message.RespondAsync("The bot extends a shower head  before dousing " + e.Message.Author.Mention + "!");
            }
            string name = e.Guild.GetMemberAsync(e.Author.Id).Result.Nickname;
            Console.WriteLine(name);
            if (name != null && name.Length > 0)
            {
                try
                {
                    string newName = name.Split('-')[0];
                    Console.WriteLine(newName);
                    await discord.GetGuildAsync(e.Guild.Id).Result.GetMemberAsync(e.Author.Id).Result.ModifyAsync(newName);
                }

                catch (NullReferenceException)
                {
                    Console.WriteLine("There was an issue where we couldn't properly split the user Id from the suffix.");
                }
            }

            else
            {
                await discord.GetGuildAsync(e.Guild.Id).Result.GetMemberAsync(e.Author.Id).Result.ModifyAsync(name);
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

            await discord.CreateDmAsync(e.Message.Author).Result.SendMessageAsync("The currently loaded messages are: \n");

            foreach (Form f in tfArray)
            {
                loadedForms.Append(f.suffix + "\n");
            }

            await discord.CreateDmAsync(e.Author).Result.SendMessageAsync(loadedForms.ToString());
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
                await discord.CreateDmAsync(e.Author).Result.SendMessageAsync("Thank you! In a channel, type !cleanup to undo a change, type !zap for a transformation, and type !test to get the local computer time!");
                await Task.Delay(1000);
                await discord.CreateDmAsync(e.Author).Result.SendMessageAsync("Type in a channel !list for all of the loaded forms!");
                await Task.Delay(1000);
                await discord.CreateDmAsync(e.Author).Result.SendMessageAsync("All TFs have a 10 second cooldown. Please do not spam the bot.");


            }

            catch (Exception whoops)
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
                if (e.Guild.GetMemberAsync(e.Message.Author.Id).Result.Nickname == null ||
                    e.Guild.GetMemberAsync(e.Message.Author.Id).Result.Nickname.Length == 0)
                {
                    string newDescriptor = form_description.Replace("@user", e.Message.Author.Username);
                    await e.Message.RespondAsync(newDescriptor);
                    //await discord.ModifyMember(e.Guild.Id, e.Message.Author.Id, e.Message.Author.Username + suffix);
                    await discord.GetGuildAsync(e.Guild.Id).Result.GetMemberAsync(e.Author.Id).Result.ModifyAsync(e.Message.Author.Username + suffix);
                }
                else
                {
                    //await e.Message.RespondAsync(form_description.Replace("@user", e.Guild.GetMember(e.Author.Id).Result.Nickname));
                    await e.Message.RespondAsync(form_description.Replace("@user", e.Guild.GetMemberAsync(e.Author.Id).Result.Nickname));

                    if (((e.Guild.GetMemberAsync(e.Message.Author.Id).Result.Nickname + suffix).Length) <= 32) //we check to ensure that the new nickname is less than or equal to the size of the discord name length max.
                    {
                        //await discord.ModifyMember(e.Guild.Id, e.Message.Author.Id, e.Guild.GetMember(e.Author.Id).Result.Nickname + suffix);
                        await discord.GetGuildAsync(e.Guild.Id).Result.GetMemberAsync(e.Author.Id).Result.ModifyAsync(
                            discord.GetGuildAsync(e.Guild.Id).Result.GetMemberAsync(e.Author.Id).Result.Nickname + suffix);
                    }

                    else //if not, we pull from the user's main name.
                    {
                        string newDescriptor = form_description.Replace("@user", e.Message.Author.Username);
                        await e.Message.RespondAsync(newDescriptor);
                        //await discord.ModifyMember(e.Guild.Id, e.Message.Author.Id, e.Message.Author.Username + suffix);
                        await discord.GetGuildAsync(e.Guild.Id).Result.GetMemberAsync(e.Author.Id).Result.ModifyAsync(
                            discord.GetGuildAsync(e.Guild.Id).Result.GetMemberAsync(e.Author.Id).Result.Username + suffix);
                    }
                }

            }

            catch (Exception whoops) //This code catches everything. The log is left on the running platform so we can catch exceptions, 
            //And tells the end user to contact the author of the code.
            {
                await discord.CreateDmAsync(e.Author).Result.SendMessageAsync("Something went really wrong! Contact Xilats!");
                Console.WriteLine("An exception was caught: " + whoops);
                await Task.Yield();
            }

            /*
            finally
            {
                await discord.CreateDmAsync(e.Message.Author).Result.SendMessageAsync("10 second cooldown engaged. Please do not spam the bot!");
            }
            */
        }

        public static async Task Run()
        {

            //We are using a try here because the bot tends to not deal with weird connection events well.
            //As such, if an error is detected, we assume that we want to reconnect.
            //We wait for the bot admin to hit any key to stop the reconnect process.
            var discord = new DiscordClient(new DiscordConfig
            {
                AutoReconnect = true,
                DiscordBranch = Branch.Stable,
                LargeThreshold = 250,
                LogLevel = LogLevel.Warning,
                Token = oauth,
                TokenType = TokenType.Bot,
                UseInternalLogHandler = false
                
            });

            //Start the checker.
            cooldownLock = true;

            if (!started)
            {
                cool = new Thread(new ThreadStart(CoolDownCheck));
                cool.Start();
            }

            discord.DebugLogger.LogMessageReceived += async (o, e) =>
            {
                Console.WriteLine($"[{e.Timestamp}] [{e.Application}] [{e.Level}] {e.Message}");
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
                    await e.Message.RespondAsync("The time is " + DateTime.Now);
                }

                if (e.Message.Content.ToLower() == "!help")
                {
                    await HelpMessage(discord, e);
                }

                if (e.Message.Content.ToLower() == "!list")
                {
                    await ListForms(discord, e);
                }
                if (!cooldowntank.ContainsKey(e.Author.Id))
                {
                    if (e.Message.Content.ToLower() == "!cleanup" && !e.Channel.IsPrivate)
                    {
                        await Cleanup(discord, e, true);
                        usercooldownIndex.Add(e.Author.Id);
                        cooldowntank.Add(e.Author.Id,
                            new CoolingUser(e.Author.Id));
                        }

                    if (e.Message.Content.ToLower() == "!zap" && !e.Channel.IsPrivate)
                    {
                        await e.Message.RespondAsync("The bot vibrates before blasting " + e.Message.Author.Mention + "!");

                        Random rand = new Random();
                        int form = rand.Next(tfArray.Length);

                        try
                        {
                            await FormSetter(discord, e, tfArray[form].description, tfArray[form].suffix);
                            form = rand.Next(tfArray.Length);
                        }

                        catch (Exception whoops)
                        {
                            Console.WriteLine("An exception was caught->\n " + whoops);
                        }

                        finally
                        {
                            usercooldownIndex.Add(e.Author.Id);
                            cooldowntank.Add(e.Author.Id,
                                new CoolingUser(e.Author.Id));
                                //await CoolDownCheck();
                            }
                    }

                    else
                    {
                            //Console.WriteLine("User is still in time out!");
                     }

                }

            };

            await discord.ConnectAsync();
            //await CoolDownCheck();
            await Task.Delay(-1);

        }

    }
}
