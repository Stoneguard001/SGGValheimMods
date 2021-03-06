using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
//using System;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.IO;
using Steamworks;
using System.Net;

namespace SGGValheimMod
{
    public enum ServerTypes { JoinIP = 0, Friends = 1, Community = 2 }

    //legacy class no longer used
    public class CharacterToIP
    {
        public string Character { get; set; }
        public string LastIP { get; set; }
    }

    //character to server data to be stored in the config file
    public class CharacterHost
    {
        public string Character { get; set; }
        public string IPHost { get; set; }
        public string Password { get; set; }
        public ServerTypes ServerType { get; set; }
        public string ServerName { get; set; }
        public ulong SteamHostID { get; set; }
        public SteamNetworkingIPAddr SteamHostAddr { get; set; }
        public bool RequirePassword { get; set; }
    }

    //list of characters and servers. manipulated by character for Favorites and Join IPs.
    public class CharacterHostList
    {
        public const int MOD_CURRENT_VERSION = 2;
        private ConfigEntry<int> _modVersion;
        private ConfigEntry<string> _CharacterToIPList;
        private List<CharacterHost> _allHosts;
        public CharacterHost CurrentSelected = null;

        public int ModVersion
        {
            get
            {
                return _modVersion.Value;
            }
            set
            {
                _modVersion.Value = value;
            }

        }

        public string Character
        {
            get
            {
                return PlayerPrefs.GetString("profile");
            }
        }
        public void Init(ConfigFile config)
        {
            _modVersion = config.Bind<int>("General", "SGGValheimModIntVersion", 1, "Internal version of SGG Valheim mod"); //assume version 1
            _CharacterToIPList = config.Bind<string>("General", "CharacterToIPList", "", "List of Characters and their last IP connection");
        }

        public List<CharacterHost> AllHosts
        {
            get
            {
                if (_allHosts == null)
                {
                    //Debug.Log("Getting list of all hosts ");
                    _allHosts = DeserializeList(); //for simplicity, i am keeping the list in a serialized string in the configuration file.
                }
                return _allHosts;
            }

        }
        public List<CharacterHost> Current
        {
            get
            {
                SGGValheimMod.Log("Getting hosts for : " + Character);
                return AllHosts.FindAll(x => x.Character == Character);
            }
        }

        public CharacterHost LastJoinIP()
        {
            return Current.FindLast(x => x.ServerType == ServerTypes.JoinIP);
        }

        public string LastJoinHost()
        {
            string ret = "";
            SGGValheimMod.Log("Getting last IP for current character");
            CharacterHost charToIP = LastJoinIP();
            if (charToIP != null)
                ret = charToIP.IPHost;
            return ret;
        }

        public void AddFavorite(ServerData server)
        {
            //first check to see if it already exists. if so, update it
            CharacterHost charToIP = FindServer(server);// Current.Find(x => x.ServerName == server.m_name);
            if (charToIP == null)
            {
                charToIP = new CharacterHost();
                AllHosts.Add(charToIP);
            }

            charToIP.Character = Character;
            charToIP.ServerName = server.m_name;
            charToIP.ServerType = ServerTypes.Community;
            charToIP.SteamHostID = server.m_steamHostID;
            charToIP.SteamHostAddr = server.m_steamHostAddr;
            charToIP.RequirePassword = server.m_password;

            SaveChanges();
        }

        public CharacterHost FindServer(ServerData server)
        {
            CharacterHost charToIP = Current.Find(x => x.ServerName == server.m_name || x.IPHost == server.m_name);
            return charToIP;
        }
        public CharacterHost FindServer(string server)
        {
            CharacterHost charToIP = Current.Find(x => x.IPHost == server);
            return charToIP;
        }

        public void Remove(ServerData server)
        {
            CharacterHost charToIP = FindServer(server);
            if (charToIP != null)
            {
                AllHosts.Remove(charToIP);
                SaveChanges();
            }
        }

        public bool SetSelected(ServerData server)
        {
            CharacterHost charToIP = FindServer(server);
            if (charToIP != null)
            {
                CurrentSelected = charToIP;
                return true;
            }
            CurrentSelected = null;
            return false;
        }

        public List<ServerData> Favorites()
        {
            List<ServerData> ret = new List<ServerData>();

            var favList = Current.OrderBy(x => x.ServerType).ToList() ;
            int iplist = 0;
            favList.ForEach(item =>
            {
                ServerData newItem = new ServerData()
                {
                    m_name = string.IsNullOrEmpty(item.ServerName) ? item.IPHost : item.ServerName,
                    m_steamHostID = item.SteamHostID,
                    m_steamHostAddr = item.SteamHostAddr,
                    m_password = item.RequirePassword
                };

                if (item.ServerType == ServerTypes.JoinIP)
                {
                    newItem.m_steamHostAddr.SetIPv4((uint)iplist++, 0);
                }

                ret.Add(newItem);
            });
            return ret;
        }

        // replaced with AddJoinIP
        //public void SetJoinIP(string currentIP)
        //{
        //    SGGValheimMod.Log("Setting Join IP for : " + Character + " to " + currentIP);
        //    List<CharacterHost> characterToIPList = AllHosts;
        //    CharacterHost charToIP = characterToIPList.Find(x => x.Character == Character && x.ServerType == ServerTypes.JoinIP); //if we find the character and server type, set their last IP.
        //    if (charToIP != null)
        //    {
        //        charToIP.IPHost = currentIP;
        //    }
        //    else
        //    {  // no character found, add them to the list of characters. 
        //        charToIP = new CharacterHost() { Character = Character, IPHost = currentIP, ServerType = ServerTypes.JoinIP };
        //        characterToIPList.Add(charToIP);
        //    }

        //    CurrentSelected = charToIP;
        //    SaveChanges();
        //}

        public void AddJoinIP(string currentIP)
        {
            SGGValheimMod.Log("Setting Join IP for : " + Character + " to " + currentIP);
            CharacterHost charToIP = FindServer(currentIP);
            if (charToIP != null) //already exists, no need to resave
            {
                CurrentSelected = charToIP;
                return;
            }

            charToIP = new CharacterHost() { Character = Character, IPHost = currentIP, ServerType = ServerTypes.JoinIP };
            AllHosts.Add(charToIP);
            CurrentSelected = charToIP;
            SaveChanges();
        }

        public void UpdatePassword(string pwd)
        {
            if (CurrentSelected != null)
            {
                CurrentSelected.Password = pwd;
                CurrentSelected.RequirePassword = true;
                SaveChanges();
            }
        }

        public void SaveChanges()
        {
            _modVersion.Value = MOD_CURRENT_VERSION;
            SerializeList(AllHosts); //put the list back int the configuration file
        }

        #region private functions
        private void SerializeList(List<CharacterHost> charToIPList)
        {
            string str;
            XmlSerializer xmlSer = new XmlSerializer(charToIPList.GetType());
            using (StringWriter writer = new StringWriter())
            {
                xmlSer.Serialize(writer, charToIPList);
                str = writer.ToString();
            }
            _CharacterToIPList.Value = str;
        }

        private List<CharacterHost> DeserializeList()
        {
            List<CharacterHost> ret;

            string str = _CharacterToIPList.Value;
            if (string.IsNullOrEmpty(str))
            {
                return new List<CharacterHost>();
            }

            if (_modVersion.Value < 2) //new structure version for version 2
            {
                XmlSerializer xmlSer = new XmlSerializer(typeof(List<CharacterToIP>));
                // need to convert older data structures to new structure
                ret = new List<CharacterHost>();
                using (StringReader reader = new StringReader(str))
                {
                    var tmp = (List<CharacterToIP>)xmlSer.Deserialize(reader);
                    ret = tmp.ConvertAll(t => new CharacterHost { Character = t.Character, IPHost = t.LastIP, ServerType = ServerTypes.JoinIP });
                }
            }
            else
            {
                XmlSerializer xmlSer = new XmlSerializer(typeof(List<CharacterHost>));
                using (StringReader reader = new StringReader(str))
                {
                    ret = (List<CharacterHost>)xmlSer.Deserialize(reader);
                }
            }

            return ret;
        }
        #endregion
    }
}
