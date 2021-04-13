using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
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
        class IP_Patch
        {
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