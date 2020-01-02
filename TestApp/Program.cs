using TetraAPI.Client;
using TetraAPI.Handlers;
using TetraAPI.Server;
using System;
namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            string g = "-1";
            foreach (var item in args)
            {
                switch (item)
                {
                    case "server":
                        {
                            Server server = new Server(true);
                            server.StartServer();
                        }
                        break;
                    case "client":
                        {
                            Client cli = new Client(true);
                            cli.OnConnectSuccessfully = delegate
                            {
                                if (args[1] == "create")
                                {
                                    cli.CreateUserAsync(new User()
                                    {
                                        Email = "dev@dev.dev",
                                        Password = "1234",
                                        PID = "@dev",
                                        LastSeen = DateTime.Now,
                                        Name = "Dev",
                                        Status = "Hello"
                                    });
                                }
                                else if (args[1] == "login")
                                {
                                    cli.LoginUserAsync(new User()
                                    {
                                        Email = "dev@dev.dev",
                                        Password = "1234"
                                    });
                                }
                                if (args[2] == "group")
                                {
                                    Group gr = new Group();
                                    gr.Title = "TESTGROUP";
                                    gr.Description = "PLAPLA";
                                    gr.MembersIDs = new string[] { "@dev" };
                                    cli.OnGroupInfoReceived += (s, e) =>
                                    {
                                        Console.WriteLine(e.Group);
                                        g = e.Group.ID.ToString();
                                    };
                                    cli.CreateGroupAsync(gr);
                                }
                            };
                            cli.OnLogin = (s, e) =>
                            {
                                Console.WriteLine("[EVENT] OnLogin : " + e.State);
                            };
                            cli.OnCreateUser = (s, e) =>
                            {
                                Console.WriteLine("[EVENT] OnCreateUser : " + e.State);
                            };
                            cli.OnMessageReceive = (s, e) =>
                             {
                                 switch (e.Message.ChatType)
                                 {
                                     case ChatType.Private:
                                         Console.WriteLine("[EVENT] OnMessageReceive(Private) : " + e.Message);
                                         break;
                                     case ChatType.Group:
                                         if (g != e.Message.MessageFrom)
                                         {
                                             cli.GetGroupInfoAsync(int.Parse(e.Message.MessageFrom));
                                         }
                                         Console.WriteLine("[EVENT] OnMessageReceive(Group) : " + e.Message);
                                         break;
                                 }
                             };
                            cli.OnMessageStateChange = (s, e) =>
                             {
                                 Console.WriteLine("[EVENT] OnMessageStateChange : " + e.MessageID + "R");
                             };
                            cli.OnMessageError = (s, e) =>
                            {
                                Console.WriteLine("[EVENT] OnMessageError : " + e.Error);
                            };
                            cli.OnSearchResult = (s, e) =>
                              {
                                  Console.WriteLine("[EVENT] OnSearchResult : " + string.Join(",", e.Results));
                              };
                            cli.Start();
                            if (args.Length > 2)
                            {
                                if (args[2] == "block")
                                {
                                    bool blocked = true;
                                    while (true)
                                    {
                                        Console.WriteLine("Press To Block...");
                                        Console.ReadKey();
                                        cli.SetUserBlockAsync(blocked, "@dev2");
                                        blocked = !blocked;
                                    }
                                }
                                else if (args[2] == "search")
                                {
                                    while (true)
                                    {
                                        Console.WriteLine("Type A name to search for ");
                                        string keyW = Console.ReadLine();
                                        if (keyW != "") cli.SearchForUserAsync(keyW);
                                    }
                                }
                                else if (args[2] == "group")
                                {
                                    while (true)
                                    {
                                        Console.WriteLine("Write A Message And Press Enter To Send");
                                        string message = Console.ReadLine();
                                        if (message == "add") cli.AddMemberToGroupAsync("@dev2", int.Parse(g));
                                        else if (message == "remove") cli.RemoveMemberFromGroupAsync("@dev2", int.Parse(g));
                                        else
                                        {
                                            cli.SendMessageAsync(new Message()
                                            {
                                                MessageContent = message,
                                                MessageDate = DateTime.Now,
                                                MessageFrom = "@dev",
                                                MessageTo = g,
                                                ChatType = ChatType.Group
                                            });
                                        }
                                    }

                                }
                                else if (args[2] == "file")
                                {
                                    while (true)
                                    {
                                        Console.WriteLine("Write A Message And Press Enter To Send");
                                        string message = Console.ReadLine();
                                        cli.SendFile("testfile.txt", "dev2");
                                    }

                                }
                            }
                            else
                            {
                                while (true)
                                {
                                    Console.WriteLine("Write A Message And Press Enter To Send");
                                    string message = Console.ReadLine();
                                    cli.SendMessageAsync(new Message()
                                    {
                                        MessageContent = message,
                                        MessageDate = DateTime.Now,
                                        MessageFrom = "@dev",
                                        MessageTo = "@dev2",
                                        ChatType = ChatType.Private
                                    });
                                }
                            }
                        }
                        break;
                    case "client2":
                        {
                            Client cli = new Client(true);
                            cli.OnConnectSuccessfully += delegate
                            {
                                cli.CreateUserAsync(new User()
                                {
                                    Email = "dev@dev.dev",
                                    Password = "1234",
                                    PID = "@dev",
                                    LastSeen = DateTime.Now,
                                    Name = "Dev",
                                    Status = "Hello"
                                });
                            };
                            cli.OnCreateUser += (s, e) =>
                            {
                                Console.WriteLine("[EVENT] OnCreateUser : " + e.State);
                            };
                            cli.OnMessageReceive += (s, e) =>
                            {
                                Console.WriteLine("[EVENT] OnMessageReceive : " + e.Message);
                            };
                            cli.OnMessageStateChange += (s, e) =>
                            {
                                Console.WriteLine("[EVENT] OnMessageStateChange : " + e.MessageID + "R");
                            };
                            cli.Start();
                            while (true)
                            {
                                Console.WriteLine("Write A Message And Press Enter To Send");
                                string message = Console.ReadLine();
                                cli.SendMessageAsync(new Message()
                                {
                                    MessageContent = message,
                                    MessageDate = DateTime.Now,
                                    MessageFrom = "@dev",
                                    MessageTo = "@dev2"
                                });
                            }
                        }
                    case "client3":
                        {
                            Client cli = new Client(true);
                            cli.OnConnectSuccessfully += delegate
                            {
                                cli.CreateUserAsync(new User()
                                {
                                    Email = "dev2@dev.dev",
                                    Password = "1234",
                                    PID = "@dev2",
                                    LastSeen = DateTime.Now,
                                    Name = "Dev",
                                    Status = "Hello"
                                });
                            };
                            cli.OnCreateUser += (s, e) =>
                            {
                                Console.WriteLine("[EVENT] OnCreateUser : " + e.State);
                            };
                            cli.OnMessageReceive += (s, e) =>
                            {
                                Console.WriteLine("[EVENT] OnMessageReceive : " + e.Message);
                                cli.GetUserInfoAsync(e.Message.MessageFrom);
                            };
                            cli.OnMessageStateChange += (s, e) =>
                            {
                                Console.WriteLine("[EVENT] OnMessageStateChange : " + e.MessageID + "R");
                            };
                            cli.OnUserInfoReceived += (s, e) =>
                            {
                                Console.WriteLine(e.User);
                            };
                            cli.Start();
                            while (true)
                            {
                                Console.WriteLine("Write A Message And Press Enter To Send");
                                string message = Console.ReadLine();
                                cli.SendMessageAsync(new Message()
                                {
                                    MessageContent = message,
                                    MessageDate = DateTime.Now,
                                    MessageFrom = "@dev2",
                                    MessageTo = "@dev"
                                });
                            }
                        }
                }
            }
        }
    }
}
