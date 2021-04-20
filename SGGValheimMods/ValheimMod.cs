using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using BepInEx.Configuration;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.IO;

namespace SGGValheimMod
{

    [BepInPlugin("stoneguardiangames.SGGValheimMod", "SGG Valheim Mod", "0.3.0")]
    [BepInProcess("valheim.exe")]
    public class SGGValheimMod : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony("stoneguardiangames.SGGValheimMod");
        private static CharacterHostList _hostList = new CharacterHostList();
        void Awake()
        {
            _hostList.Init(Config);
            harmony.PatchAll();
        }

        void OnDestroy()
        {
            harmony.UnpatchSelf();
        }

        [HarmonyPatch]
        class FejdStartup_Patch
        {
            static GameObject m_serversTab = null;
            static GameObject m_favesTab = null;
            static Toggle m_SaveToFaves = null;
            static Button m_RemoveFrFaves = null;
            static List<ServerData> m_serverList = null;
            static FejdStartup m_fejdStartup = null;
            static ServerData m_joinServer = null;

            [HarmonyPatch(typeof(FejdStartup), "SetupGui")]
            static void Postfix(ref FejdStartup __instance)
            {
                m_fejdStartup = __instance;
            }

            [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.OnJoinIPOpen))]
            static void Prefix(ref InputField ___m_joinIPAddress)
            {
                ___m_joinIPAddress.text = _hostList.LastJoinHost(); // get last IP this character joined, if there is one;
                Debug.Log("field set to " + ___m_joinIPAddress.text);
            }

            [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.OnJoinIPConnect))]
            static void Postfix(ref InputField ___m_joinIPAddress)
            {
                _hostList.SetJoinIP(___m_joinIPAddress.text); //save the last entered IP address.
            }

            [HarmonyPatch(typeof(FejdStartup), "ShowStartGame")]
            static void Postfix(GameObject ___m_startGamePanel, ref List<ServerData> ___m_serverList)
            {
                m_serverList = ___m_serverList;

                if (m_favesTab == null)
                {
                    Debug.Log("Favorites mod - adding buttons");
                    Transform joinObject = findFirstObject(___m_startGamePanel, "Join");
                    var rect = joinObject.GetComponent<RectTransform>().rect;
                    m_favesTab = newObject(joinObject, "Faves", "Favorites", joinObject.parent, joinObject.position, (int)rect.width+2, 2);
                    //m_favesTab = newObject(joinObject, "Faves", "Favorites", joinObject.parent, joinObject.position, 180, 2);
                    m_serversTab = joinObject.gameObject;

                    Button btn = m_favesTab.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick = new Button.ButtonClickedEvent();
                        btn.onClick.AddListener(OnFavesTab);
                    }

                    Transform serverPanel = findFirstObject(m_fejdStartup.m_serverListPanel, "PublicGames");
                    GameObject saveAsFave = newObject(serverPanel, "SaveToFaves", "Save to favorites", serverPanel.parent, serverPanel.position, 190);

                    m_SaveToFaves = saveAsFave.GetComponent<Toggle>();
                    if (m_SaveToFaves != null)
                    {
                        m_SaveToFaves.group = null;
                    }

                    Transform servbackObj = findFirstObject(m_fejdStartup.m_serverListPanel, "Back");
                    saveAsFave = newObject(servbackObj, "RemoveFave", "Remove favorite", serverPanel.parent, m_SaveToFaves.transform.position);

                    m_RemoveFrFaves = saveAsFave.GetComponent<Button>();
                    if (m_RemoveFrFaves != null)
                    {
                        m_RemoveFrFaves.onClick = new Button.ButtonClickedEvent();
                        m_RemoveFrFaves.onClick.AddListener(OnRemoveFave);
                        m_RemoveFrFaves.gameObject.SetActive(false);
                    }
                }

                if (_hostList.Favorites().Count > 0) //force the load of favorites, then go back to the Start Game tab
                {
                    OnFavesTab();
                    Transform hostObject = findFirstObject(___m_startGamePanel, "Host");
                    if (hostObject != null)
                    {
                        Button btn = hostObject?.GetComponent<Button>();
                        btn.onClick.Invoke();
                    }
                }
            }

            [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.OnSelectWorldTab))]
            static void Postfix()
            {
                if (m_RemoveFrFaves != null)
                {
                    m_RemoveFrFaves.gameObject.SetActive(false);
                    m_SaveToFaves.gameObject.SetActive(true);
                }
            }

            [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.OnJoinStart))]
            static void Prefix(ref ServerData ___m_joinServer)
            {
                if (m_SaveToFaves != null)
                {
                    if (m_SaveToFaves.isOn)
                    {
                        _hostList.AddFavorite(___m_joinServer);
                       Debug.Log("saved to faves");
                    }
                }
            }

            [HarmonyPatch(typeof(FejdStartup), "OnSelectedServer")]
            static void Postfix(ServerData ___m_joinServer)
            {
                //Log("server select");
                m_joinServer = ___m_joinServer;
                _hostList.SetSelected(m_joinServer);
            }

            private static GameObject newObject(Transform original, string name, string text, Transform parent, Vector3 position, int xoffset = 0, int yoffset = 0)
            {
                if (original == null)
                    return new GameObject();

                GameObject ret = Instantiate(original.gameObject);
                ret.name = "Faves";
                ret.transform.SetParent(parent, false);
                ret.GetComponent<RectTransform>().position = new Vector3(position.x + xoffset, position.y + yoffset);
                ret.GetComponentInChildren<Text>().text = text;

                return ret;
            }

            private static Transform findFirstObject(GameObject baseObject, string itemName)
            {
                Transform[] list = baseObject.GetComponentsInChildren<Transform>();
                Transform ret = null;
                foreach (Transform item in list)
                {
                    //Log("item named :: " + item.name + " >> " + item.parent);
                    if (item.name == itemName) //this loop is intentionally a little complex for looking at all the objects in the GameObject. Uncomment line above and comment out return line for that purpose.
                    {
                        ret = item;
                        return ret;
                    }
                }

                return ret;
            }

            private static void OnFavesTab()
            {
                Log("faves clicked");

                m_RemoveFrFaves.gameObject.SetActive(true);
                m_SaveToFaves.gameObject.SetActive(false);

                m_fejdStartup.m_serverRefreshButton.interactable = false;
                ZSteamMatchmaking.instance.StopServerListing();

                m_serverList.Clear();
                m_serverList.AddRange(_hostList.Favorites());

                var btn = m_serversTab.GetComponent<Button>();
                btn.onClick.Invoke();
            }

            private static void OnRemoveFave()
            {
                if (m_joinServer != null)
                {
                    Log("removing " + m_joinServer.m_name);
                    _hostList.Remove(m_joinServer);
                }
                OnFavesTab();
            }

            private static void Log(string text)
            {
                Debug.Log("Faves mod: " + text);
            }
        }

        [HarmonyPatch]
        class Pwd_Set
        {
            [HarmonyPatch(typeof(ZNet), "OnPasswordEnter")]
            static void Prefix(string pwd)
            {
                Debug.Log("password entered " + pwd);
                var charJIP = _hostList.CurrentSelected;
                if (charJIP != null)
                {
                    charJIP.Password = pwd;
                    charJIP.RequirePassword = true;
                    _hostList.SaveChanges();
                }
            }

            [HarmonyPatch(typeof(ZNet), "RPC_ClientHandshake")]
            static void Postfix(ref InputField ___m_passwordDialog)
            {
                Debug.Log("Pwd Field " + ___m_passwordDialog);
                var charJIP = _hostList.CurrentSelected ?? new CharacterHost();
                if (charJIP.RequirePassword || !string.IsNullOrEmpty(charJIP.Password))
                {
                    InputField componentInChildren = ___m_passwordDialog.GetComponentInChildren<InputField>();
                    componentInChildren.text = charJIP.Password;
                }
            }

        }

    }
}