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
            //lastIP = Config.Bind<string>("General", "lastIP", "", "Last IP entered to connect");
            harmony.PatchAll();
        }

        void OnDestroy()
        {
            harmony.UnpatchSelf();
        }

        [HarmonyPatch]
        class FejdStartup_Patch
        {
            static GameObject m_favesTab = null;
            static Toggle m_SaveToFaves = null;
            static GameObject m_serverListPanel = null;
            static List<ServerData> m_serverList = null;
            static List<GameObject> m_serverListElements = null;
            static FejdStartup m_instance = null;

            [HarmonyPatch(typeof(FejdStartup), "SetupGui")]
            static void Postfix(ref FejdStartup __instance)
            {
                m_instance = __instance;
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

            [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.OnCharacterStart))]
            static void Postfix(GameObject ___m_startGamePanel, GameObject ___m_serverListPanel, ref List<ServerData> ___m_serverList, ref List<GameObject> ___m_serverListElements)
            {
                m_serverListPanel = ___m_serverListPanel;
                m_serverList = ___m_serverList;
                m_serverListElements = ___m_serverListElements;

                if (m_favesTab == null)
                {
                    var list = ___m_startGamePanel.GetComponentsInChildren<Transform>();
                    //Debug.Log("children count " + list.Length);

                    //var item = ___m_startGamePanel..Find("Join");
                    //Debug.Log("found " + item.name);
                    foreach (Transform item in list)
                    {
                        if (item.name == "Join" && m_favesTab == null) //there may be more than one Join
                        {
                            m_favesTab = Instantiate(item.gameObject);
                            m_favesTab.name = "Faves";
                            //Debug.Log("component " + m_favesTab.name);
                           
                            m_favesTab.transform.SetParent(item.transform.parent);
                            m_favesTab.GetComponent<RectTransform>().position = new Vector3(item.transform.position.x + 180, item.transform.position.y+2);
                            //var rect = m_favesTab.GetComponent<RectTransform>().rect;
                            //rect = new Rect(m_favesTab.transform.position, new Vector2(rect.width-20, rect.height));
                            m_favesTab.GetComponentInChildren<Text>().text = "Favorites";
                            Button btn = m_favesTab.GetComponent<Button>();
                            if (btn != null)
                            {
                                Debug.Log("Button found");
                                btn.onClick.AddListener(OnFavesPressed);
                            }
                            //m_favesTab.transform.
                            //m_favesTab.transform.SetSiblingIndex(2);
                            //m_favesTab.SetActive(false);
                            //newitem.position.x += 476;
                        }
                    }

                    list = ___m_serverListPanel.GetComponentsInChildren<Transform>();

                    foreach (Transform item in list)
                    {
                        //Debug.Log("component " + item.name + ": " + item.GetType().ToString());

                        if (item.name == "PublicGames")
                        {
                            var saveAsFave = Instantiate(item.gameObject);
                            saveAsFave.name = "SaveToFaves";
                            saveAsFave.transform.SetParent(item.transform.parent);
                            saveAsFave.GetComponent<RectTransform>().position = new Vector3(item.transform.position.x + 190, item.transform.position.y);
                            saveAsFave.GetComponentInChildren<Text>().text = "Save to Favorites";

                            m_SaveToFaves = saveAsFave.GetComponent<Toggle>();
                            if (m_SaveToFaves != null)
                            {
                                Debug.Log("Toggle " + m_SaveToFaves.name);
                                m_SaveToFaves.group = null;
                            }
                        }
                    }
                }
                //___m_startGamePanel.AddComponent<TabPanel>();
            }

            private static void OnFavesPressed()
            {
                Debug.Log("faves clicked");
                m_instance.m_worldListPanel.SetActive(false);
                m_instance.m_serverListPanel.SetActive(true);

                //m_instance.OnServerListTab();
                //m_instance.OnServerFilterChanged();
                m_serverList.Clear();
                m_serverListElements.Clear();
                for (int i = 0; i < m_serverList.Count; i++)
                {
                    GameObject gameObject = Object.Instantiate(m_instance.m_serverListElement, m_instance.m_serverListRoot);
                    gameObject.SetActive(value: true);
                    (gameObject.transform as RectTransform).anchoredPosition = new Vector2(0f, (float)i * (0f - m_instance.m_serverListElementStep));
                    gameObject.GetComponent<Button>().onClick.AddListener(OnSelectedServer);
                    m_serverListElements.Add(gameObject);
                }

            }

            private static void OnSelectedServer()
            {
                GameObject currentSelectedGameObject = EventSystem.current.currentSelectedGameObject;
                //int index = FindSelectedServer(currentSelectedGameObject);
                //m_joinServer = m_serverList[index];
                //UpdateServerListGui(centerSelection: false);
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
            //[HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.OnServerListTab))]
            //static void Postfix()
            //{
            //    if (m_favesTab == null)
            //        return;

            //    //m_favesTab.SetActive(value: false);
            //}

        }

        [HarmonyPatch]
        class Pwd_Set
        {
            [HarmonyPatch(typeof(ZNet), "OnPasswordEnter")] //"OnPasswordEnter")]
            static void Prefix(string pwd)
            {
                Debug.Log("password entered " + pwd);
                var charJIP = _hostList.LastJoinIP();
                if (charJIP != null)
                {
                    charJIP.Password = pwd;
                    _hostList.SaveChanges();
                }
            }

            [HarmonyPatch(typeof(ZNet), "RPC_ClientHandshake")]
            //static void Postfix(ZRpc rpc, bool needPassword, ref InputField ___m_passwordDialog)
            static void Postfix(ref InputField ___m_passwordDialog)
            {
                Debug.Log("Pwd Field " + ___m_passwordDialog);
                //if (needPassword)
                {
                    InputField componentInChildren = ___m_passwordDialog.GetComponentInChildren<InputField>();
                    var charJIP = _hostList.LastJoinIP() ?? new CharacterHost();
                    componentInChildren.text = charJIP.Password;
                }
            }

        }

    }
}