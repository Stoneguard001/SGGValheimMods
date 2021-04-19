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
            static List<ServerData> m_serverList = null;
            static FejdStartup m_fejdStartup = null;

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
                    Transform joinObject = findFirstObject(___m_startGamePanel, "Join");
                    if (joinObject != null)
                    {
                        m_serversTab = joinObject.gameObject;
                        m_favesTab = Instantiate(joinObject.gameObject);
                        m_favesTab.name = "Faves";
                        m_favesTab.transform.SetParent(joinObject.transform.parent);
                        m_favesTab.GetComponent<RectTransform>().position = new Vector3(joinObject.transform.position.x + 180, joinObject.transform.position.y + 2);
                        m_favesTab.GetComponentInChildren<Text>().text = "Favorites";

                        Button btn = m_favesTab.GetComponent<Button>();
                        if (btn != null)
                        {
                            Debug.Log("Faves button and listener added");

                            btn.onClick.AddListener(OnFavesTab);
                        }
                    }

                    Transform serverPanel = findFirstObject(m_fejdStartup.m_serverListPanel, "PublicGames");
                    if (serverPanel != null)
                    {
                        var saveAsFave = Instantiate(serverPanel.gameObject);
                        saveAsFave.name = "SaveToFaves";
                        saveAsFave.transform.SetParent(serverPanel.transform.parent);
                        saveAsFave.GetComponent<RectTransform>().position = new Vector3(serverPanel.transform.position.x + 190, serverPanel.transform.position.y);
                        saveAsFave.GetComponentInChildren<Text>().text = "Save to Favorites";

                        m_SaveToFaves = saveAsFave.GetComponent<Toggle>();
                        if (m_SaveToFaves != null)
                        {
                            Debug.Log("Toggle " + m_SaveToFaves.name);
                            m_SaveToFaves.group = null;
                        }
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

            [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.OnWorldStart))]
            static void Prefix(ref World ___m_world)
            {
                Debug.Log("Starting world " + ___m_world.m_name);

                if (m_SaveToFaves != null)
                {
                    Debug.Log("save fave is " + m_SaveToFaves.isOn.ToString());
                }
            }

            [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.OnJoinStart))]
            static void Prefix(ref ServerData ___m_joinServer)
            {
                Debug.Log("Starting world " + ___m_joinServer.m_name);

                if (m_SaveToFaves != null)
                {
                    if (m_SaveToFaves.isOn)
                    {
                        _hostList.AddFavorite(___m_joinServer);
                       Debug.Log("saved to faves");
                    }
                }
            }
            private static Transform findFirstObject(GameObject baseObject, string itemName)
            {
                Transform[] list = baseObject.GetComponentsInChildren<Transform>();
                Transform ret = null;
                foreach (Transform item in list)
                {
                    //Debug.Log("item named :: " + item.name + " >> " + item.parent);
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
                Debug.Log("faves clicked");

                m_fejdStartup.m_serverRefreshButton.interactable = false;
                ZSteamMatchmaking.instance.StopServerListing();

                m_serverList.Clear();
                m_serverList.AddRange(_hostList.Favorites());

                var btn = m_serversTab.GetComponent<Button>();
                btn.onClick.Invoke();
            }
        }

        [HarmonyPatch]
        class Pwd_Set
        {
            [HarmonyPatch(typeof(ZNet), "OnPasswordEnter")]
            static void Prefix(string pwd)
            {
                Debug.Log("password entered " + pwd);
                var charJIP = _hostList.LastJoinIP();
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
                var charJIP = _hostList.LastJoinIP() ?? new CharacterHost();
                if (charJIP.RequirePassword || !string.IsNullOrEmpty(charJIP.Password))
                {
                    InputField componentInChildren = ___m_passwordDialog.GetComponentInChildren<InputField>();
                    componentInChildren.text = charJIP.Password;
                }
            }

        }

    }
}