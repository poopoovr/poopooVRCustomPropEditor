using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace poopooVRCustomPropEditor.Data
{
    public class ModDatabaseFetcher : MonoBehaviour
    {
        private const string DATA_URL = "https://www.poopoovr.co.uk/data";

        public static Dictionary<string, string> KnownCheats { get; private set; } = new Dictionary<string, string>();
        public static Dictionary<string, string> KnownMods { get; private set; } = new Dictionary<string, string>();
        public static bool IsLoaded { get; private set; } = false;
        public static bool IsFetching { get; private set; } = false;

        public static event Action OnDataLoaded;

        public void FetchData()
        {
            if (!IsFetching)
            {
                StartCoroutine(FetchDataCoroutine());
            }
        }

        private IEnumerator FetchDataCoroutine()
        {
            IsFetching = true;
            Debug.Log("[ModDatabaseFetcher] Fetching mod database from server...");

            using (UnityWebRequest request = UnityWebRequest.Get(DATA_URL))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string html = request.downloadHandler.text;
                    ParseData(html);
                    IsLoaded = true;
                    Debug.Log($"[ModDatabaseFetcher] Loaded {KnownCheats.Count} cheats and {KnownMods.Count} mods from server");
                    OnDataLoaded?.Invoke();
                }
                else
                {
                    Debug.LogWarning($"[ModDatabaseFetcher] Failed to fetch data: {request.error}");
                    LoadFallbackData();
                }
            }

            IsFetching = false;
        }

        private void ParseData(string html)
        {
            try
            {
                Match preMatch = Regex.Match(html, @"<pre>([\s\S]*?)</pre>", RegexOptions.IgnoreCase);
                string json = preMatch.Success ? preMatch.Groups[1].Value : html;

                json = json.Trim();

                KnownCheats = ParseSection(json, "Known Cheats");
                KnownMods = ParseSection(json, "Known Mods");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModDatabaseFetcher] Parse error: {ex.Message}");
                LoadFallbackData();
            }
        }

        private Dictionary<string, string> ParseSection(string json, string sectionName)
        {
            var result = new Dictionary<string, string>();

            string pattern = $"\"{sectionName}\"\\s*:\\s*\\{{([^}}]*)\\}}";
            Match sectionMatch = Regex.Match(json, pattern, RegexOptions.Singleline);

            if (sectionMatch.Success)
            {
                string sectionContent = sectionMatch.Groups[1].Value;

                MatchCollection entries = Regex.Matches(sectionContent, "\"([^\"]+)\"\\s*:\\s*\"([^\"]+)\"");

                foreach (Match entry in entries)
                {
                    string key = entry.Groups[1].Value;
                    string value = entry.Groups[2].Value;

                    if (!result.ContainsKey(key))
                    {
                        result[key] = value;
                    }
                }
            }

            return result;
        }

        private void LoadFallbackData()
        {
            Debug.Log("[ModDatabaseFetcher] Loading fallback data...");

            KnownCheats = new Dictionary<string, string>
            {
                { "ObsidianMC", "Obsidian" },
                { "genesis", "Genesis" },
                { "elux", "Elux" },
                { "VioletFreeUser", "Violet Free" },
                { "Hidden Menu", "Hidden" },
                { "void", "Void" },
                { "6XpyykmrCthKhFeUfkYGxv7xnXpoe2", "CCMV2" },
                { "cronos", "Cronos" },
                { "ORBIT", "Orbit (Weeb)" },
                { "Violet On Top", "Violet" },
                { "ElixirMenu", "Elixir" },
                { "Elixir", "Elixir" },
                { "VioletPaidUser", "Violet Paid" },
                { "EmoteWheel", "Emotes" },
                { "MistUser", "Mist" },
                { "Untitled", "Untitled" },
                { "void_menu_open", "Void" },
                { "dark", "ShibaGT Dark" },
                { "oblivionuser", "Oblivion" },
                { "eyerock reborn", "EyeRock" },
                { "asteroidlite", "Asteroid Lite" },
                { "cokecosmetics", "Coke Cosmetx" },
                { "ØƦƁƖƬ", "Orbit (Weeb)" },
                { "y u lookin in here weirdoooo", "Malachi Menu Reborn" },
                { "Atlas", "Atlas" },
                { "Euphoric", "Euphoria" },
                { "CurrentEmote", "Vortex Emotes" },
                { "Explicit", "Explicit Menu" }
            };

            KnownMods = new Dictionary<string, string>
            {
                { "GFaces", "GFaces" },
                { "github.com/maroon-shadow/SimpleBoards", "Simple Boards" },
                { "github.com/ZlothY29IQ/GorillaMediaDisplay", "Gorilla Media Display" },
                { "GTrials", "GTrials" },
                { "github.com/ZlothY29IQ/TooMuchInfo", "TMI Zlothy" },
                { "github.com/ZlothY29IQ/RoomUtils-IW", "Room Utils" },
                { "github.com/ZlothY29IQ/MonkeClick", "Monke Click" },
                { "github.com/ZlothY29IQ/MonkeClick-CI", "Monke Click CI" },
                { "github.com/ZlothY29IQ/MonkeRealism", "Monke Realism" },
                { "MediaPad", "Media Pad" },
                { "GorillaCinema", "GCinema" },
                { "FPS-Nametags for Zlothy", "FPS Nametags" },
                { "ChainedTogetherActive", "Chained Together" },
                { "GPronouns", "GPronouns" },
                { "Fusioned", "Fusioned" },
                { "CSVersion", "Custom Skin" },
                { "github.com/ZlothY29IQ/Zloth-RecRoomRig", "Zlothy Body Estimation" },
                { "ShirtProperties", "GShirts Old" },
                { "GorillaShirts", "GShirts" },
                { "GS", "Old GShirts" },
                { "HP_Left", "Holdable Pad" },
                { "GrateVersion", "Grate" },
                { "BananaOS", "BananaOS" },
                { "GC", "GCraft" },
                { "CarName", "Vehicles" },
                { "MonkePhone", "Monke Phone" },
                { "Body Tracking", "Body Tracking" },
                { "GorillaWatch", "GWatch" },
                { "InfoWatch", "Info Watch" },
                { "Vivid", "Vivid" },
                { "BananaPhone", "Banana Phone" },
                { "CustomMaterial", "Custom Cosmetics" },
                { "cheese is gouda", "WhoIsThatMonke" },
                { "WhoIsThatMonke", "WhoIsThatMonke Recode" },
                { "WhoIsThatMonke Version", "WhoIsThatMonke" },
                { "GorillaNametags", "GNametags" },
                { "DeeTags", "Dee Tags" },
                { "Boy Do I Love Information", "BDILI" },
                { "NametagsPlusPlus", "Nametags++" },
                { "WalkSimulator", "WalkSim ZlothY Fix" },
                { "Dingus", "dingus" },
                { "Graze Heath System", "Health System" },
                { "Gorilla Track Packed", "Gorilla Track" },
                { "drowsiiiGorillaInfoBoard", "GInfo Board" },
                { "MonkeCosmetics::Material", "Monke Cosmetics" },
                { "github.com/arielthemonke/GorillaCraftAutoBuilder", "GCraft Auto Builder" },
                { "usinggphys", "GPhys" },
                { "Gorilla Track 2.3.0", "GTrack" },
                { "GorillaTorsoEstimator", "Torso Estimation" },
                { "Body Estimation", "HAN Body Estimation" },
                { "tictactoe", "TicTacToe" },
                { "ccolor", "Index" },
                { "chainedtogether", "Chained Together" },
                { "goofywalkversion", "Goofy Walk" },
                { "msp", "Monke Smart phone" },
                { "gorillastats", "Gorilla Stats" },
                { "monkehavocversion", "Monke Havoc" },
                { "silliness", "Silliness" },
                { "BoyDoILoveInformation Public", "BDILI" },
                { "DTAOI", "DTAOI" },
                { "GorillaShop", "Gorilla Shop" },
                { "DTASLOI", "DTASLOI" },
                { "GorillaChatBox", "Gorilla Chat Box" }
            };

            IsLoaded = true;
            OnDataLoaded?.Invoke();
        }
    }
}
