﻿using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using MoonSharp.Interpreter;

public class SelectOMatic : MonoBehaviour {
    private static int CurrentSelectedMod = 0;
    private static List<DirectoryInfo> modDirs;
    private Dictionary<string, Sprite> bgs = new Dictionary<string, Sprite>();
    private GameObject encounterBox;
    private GameObject devMod;
    private GameObject btnList;
    private bool animationDone = true;
    private float animationTimer = 0;

    private static float modListScroll = 0.0f;          // Used to keep track of the position of the mod list specifically. Resets if you press escape
    private static float encounterListScroll = 0.0f;    // Used to keep track of the position of the encounter list. Resets if you press escape

    private float ExitButtonAlpha = 5f;                 // Used to fade the "Exit" button in and out
    private float OptionsButtonAlpha = 5f;              // Used to fade the "Options" button in and out

    private static int selectedItem = 0;                // Used to let users navigate the mod and encounter menus with the arrow keys!

    // Use this for initialization
    private void Start() {
        GameObject.Destroy(GameObject.Find("Player"));
        GameObject.Destroy(GameObject.Find("Main Camera OW"));
        GameObject.Destroy(GameObject.Find("Canvas OW"));
        GameObject.Destroy(GameObject.Find("Canvas Two"));

        // Load directory info
        DirectoryInfo di = new DirectoryInfo(Path.Combine(FileLoader.DataRoot, "Mods"));
        var modDirsTemp = di.GetDirectories();

        // Remove mods with 0 encounters and hidden mods from the list
        List<DirectoryInfo> purged = new List<DirectoryInfo>();
        foreach (DirectoryInfo modDir in modDirsTemp) {

            // Make sure the Encounters folder exists
            if (!(new DirectoryInfo(Path.Combine(FileLoader.DataRoot, "Mods/" + modDir.Name + "/Lua/Encounters"))).Exists)
                continue;

            // Count encounters
            bool hasEncounters = false;
            foreach(FileInfo file in new DirectoryInfo(Path.Combine(FileLoader.DataRoot, "Mods/" + modDir.Name + "/Lua/Encounters")).GetFiles("*.lua")) {
                hasEncounters = true;
                break;
            }

            if (hasEncounters && (modDir.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden && !modDir.Name.StartsWith("@"))
                purged.Add(modDir);
        }
        modDirs = purged;

        // Make sure that there is at least one playable mod present
        if (purged.Count == 0) {
            GlobalControls.modDev = false;
            UnitaleUtil.DisplayLuaError("loading", "<b>Your mod folder is empty!</b>\nYou need at least 1 playable mod to use the Mod Selector.\n\n"
                + "Remember:\n1. Mods whose names start with \"@\" do not count\n2. Folders without encounter files do not count");
            return;
        }

        modDirs.Sort(delegate(DirectoryInfo a, DirectoryInfo b) {
            return a.Name.CompareTo(b.Name);
        });

        // Bind button functions
        GameObject.Find("BtnBack").GetComponent<Button>().onClick.RemoveAllListeners();
        GameObject.Find("BtnBack").GetComponent<Button>().onClick.AddListener(() => {
            if (animationDone) {
                modFolderSelection();
                ScrollMods(-1);
            }
            });
        GameObject.Find("BtnNext").GetComponent<Button>().onClick.RemoveAllListeners();
        GameObject.Find("BtnNext").GetComponent<Button>().onClick.AddListener(() => {
            if (animationDone) {
                modFolderSelection();
                ScrollMods( 1);
            }
            });

        // Grab the encounter selection box
        if (encounterBox == null)
            encounterBox = GameObject.Find("ScrollWin");
        // Grab the devMod box
        if (devMod == null)
            devMod = GameObject.Find("devMod");
        // Grab the mod list button, and give it a function
        if (btnList == null)
            btnList = GameObject.Find("BtnList");
        btnList.GetComponent<Button>().onClick.RemoveAllListeners();
        btnList.GetComponent<Button>().onClick.AddListener(() => {
            if (animationDone)
                modFolderMiniMenu();
            });
        // Grab the exit button, and give it some functions
        GameObject.Find("BtnExit").GetComponent<Button>().onClick.RemoveAllListeners();
        GameObject.Find("BtnExit").GetComponent<Button>().onClick.AddListener(() => {SceneManager.LoadScene("Disclaimer");});

        // Add devMod button functions
        if (GlobalControls.modDev) {
            GameObject.Find("BtnOptions").GetComponent<Button>().onClick.RemoveAllListeners();
            GameObject.Find("BtnOptions").GetComponent<Button>().onClick.AddListener(() => {SceneManager.LoadScene("Options");});
        }

        // Crate Your Frisk initializer
        if (GlobalControls.crate) {
            //Exit button
            foreach (Text txt in GameObject.Find("BtnExit").GetComponentsInChildren<Text>())
                txt.text = "← BYEE";

            //Options button
            if (GlobalControls.modDev)
                foreach (Text txt in GameObject.Find("BtnOptions").GetComponentsInChildren<Text>())
                    txt.text = "OPSHUNZ →";

            //Back button
            GameObject back = encounterBox.transform.Find("ScrollCutoff/Content/Back").gameObject;
            back.transform.Find("Text").GetComponent<Text>().text = "← BCAK";

            //Mod list button
            btnList.transform.Find("Label").gameObject.GetComponent<Text>().text = "MDO LITS";
            btnList.transform.Find("LabelShadow").gameObject.GetComponent<Text>().text = "MDO LITS";
        }

        // This check will be true if we just exited out of an encounter
        // If that's the case, we want to open the encounter list so the user only has to click once to re enter
        modFolderSelection();
        if (StaticInits.ENCOUNTER != "") {
            //Check to see if there is more than one encounter in the mod just exited from
            List<string> encounters = new List<string>();
            DirectoryInfo di2 = new DirectoryInfo(Path.Combine(FileLoader.ModDataPath, "Lua/Encounters"));
            foreach (FileInfo f in di2.GetFiles("*.lua")) {
                if (encounters.Count < 2)
                    encounters.Add(Path.GetFileNameWithoutExtension(f.Name));
            }

            if (encounters.Count > 1) {
                // Highlight the chosen encounter whenever the user exits the mod menu
                int temp = selectedItem;
                encounterSelection();
                selectedItem = temp;
                encounterBox.transform.Find("ScrollCutoff/Content").GetChild(selectedItem).GetComponent<MenuButton>().StartAnimation(1);
            }

            // Move the scrolly bit to where it was when the player entered the encounter
            encounterBox.transform.Find("ScrollCutoff/Content").gameObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, encounterListScroll);

            // Start the Exit button at half transparency
            ExitButtonAlpha = 0.5f;
            GameObject.Find("BtnExit/Text").GetComponent<Text>().color       = new Color(1f, 1f, 1f, 0.5f);
            GameObject.Find("BtnExit/TextShadow").GetComponent<Text>().color = new Color(0f, 0f, 0f, 0.5f);

            // Start the Options button at half transparency
            if (GlobalControls.modDev) {
                OptionsButtonAlpha = 0.5f;
                GameObject.Find("BtnOptions/Text").GetComponent<Text>().color       = new Color(1f, 1f, 1f, 0.5f);
                GameObject.Find("BtnOptions/TextShadow").GetComponent<Text>().color = new Color(0f, 0f, 0f, 0.5f);
            }

            // Reset it to let us accurately tell if the player just came here from the Disclaimer scene or the Battle scene
            StaticInits.ENCOUNTER = "";
        // Player is coming here from the Disclaimer scene
        } else {
            // When the player enters from the Disclaimer screen, reset stored scroll positions
            modListScroll = 0.0f;
            encounterListScroll = 0.0f;
        }
    }

    // A special function used specifically for error handling
    // It re-generates the mod list, and selects the first mod
    // Used for cases where the player selects a mod or encounter that no longer exists
    private void HandleErrors() {
        Debug.LogWarning("Error detected! Mod or Encounter not found! Resetting mod list...");
        CurrentSelectedMod = 0;
        bgs = new Dictionary<string, Sprite>();
        Start();
    }

    IEnumerator LaunchMod() {
        // First: make sure the mod is still here and can be opened
        if (!(new DirectoryInfo(Path.Combine(FileLoader.DataRoot, "Mods/" + modDirs[CurrentSelectedMod].Name + "/Lua/Encounters/"))).Exists
         || !File.Exists(Path.Combine(FileLoader.DataRoot, "Mods/" + modDirs[CurrentSelectedMod].Name + "/Lua/Encounters/" + StaticInits.ENCOUNTER + ".lua"))) {
            HandleErrors();
            yield break;
        }

        // Dim the background to indicate loading
        GameObject.Find("ModBackground").GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1875f);

        // Store the current position of the scrolly bit
        encounterListScroll = encounterBox.transform.Find("ScrollCutoff/Content").gameObject.GetComponent<RectTransform>().anchoredPosition.y;

        yield return new WaitForEndOfFrame();
        StaticInits.Initialized = false;
        try {
            StaticInits.InitAll();
            Debug.Log("Loading " + StaticInits.ENCOUNTER);
            GlobalControls.isInFight = true;
            SceneManager.LoadScene("Battle");
        } catch {
            GameObject.Find("ModBackground").GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.25f);
        }
    }

    // Shows a mod's "page".
    private void ShowMod(int id) {
        // Error handler
        // If current index is now out of range OR currently selected mod is not present:
        if (CurrentSelectedMod < 0 || CurrentSelectedMod > modDirs.Count - 1
            || !(new DirectoryInfo(Path.Combine(FileLoader.DataRoot, "Mods/" + modDirs[CurrentSelectedMod].Name + "/Lua/Encounters"))).Exists
            ||  (new DirectoryInfo(Path.Combine(FileLoader.DataRoot, "Mods/" + modDirs[CurrentSelectedMod].Name + "/Lua/Encounters"))).GetFiles("*.lua").Length == 0) {
            HandleErrors();
            return;
        }

        // Update currently selected mod folder
        StaticInits.MODFOLDER = modDirs[id].Name;

        // Make clicking the background go to the encounter select screen
        GameObject.Find("ModBackground").GetComponent<Button>().onClick.RemoveAllListeners();
        GameObject.Find("ModBackground").GetComponent<Button>().onClick.AddListener(() => {
            if (animationDone)
                    encounterSelection();
            });

        // Update the background
        var ImgComp = GameObject.Find("ModBackground").GetComponent<Image>();
        // First, check if we already have this mod's background loaded in memory
        if (bgs.ContainsKey(modDirs[CurrentSelectedMod].Name)) {
            ImgComp.sprite = bgs[modDirs[CurrentSelectedMod].Name];
        } else {
            // if not, find it and store it
            try {
                Sprite thumbnail = SpriteUtil.FromFile(FileLoader.pathToModFile("Sprites/preview.png"));
                ImgComp.sprite = thumbnail;
            } catch {
                try {
                    Sprite bg = SpriteUtil.FromFile(FileLoader.pathToModFile("Sprites/bg.png"));
                    ImgComp.sprite = bg;
                } catch { ImgComp.sprite = SpriteUtil.FromFile("Sprites/black.png"); }
            }
            bgs.Add(modDirs[CurrentSelectedMod].Name, ImgComp.sprite);
        }

        // Get all encounters in the mod's Encounters folder
        List<string> encounters = new List<string>();
        DirectoryInfo di = new DirectoryInfo(Path.Combine(FileLoader.ModDataPath, "Lua/Encounters"));
        foreach (FileInfo f in di.GetFiles("*.lua"))
            encounters.Add(Path.GetFileNameWithoutExtension(f.Name));

        // Update the text
        GameObject.Find("ModTitle").GetComponent<Text>().text = modDirs[id].Name;
        // Crate your frisk version
        if (GlobalControls.crate)
            GameObject.Find("ModTitle").GetComponent<Text>().text = Temmify.Convert(modDirs[id].Name, true);
        GameObject.Find("ModTitleShadow").GetComponent<Text>().text = GameObject.Find("ModTitle").GetComponent<Text>().text;

        // List # of encounters, or name of encounter if there is only one
        if (encounters.Count == 1) {
            GameObject.Find("EncounterCount").GetComponent<Text>().text = encounters[0];
            // crate your frisk version
            if (GlobalControls.crate)
                GameObject.Find("EncounterCount").GetComponent<Text>().text = Temmify.Convert(encounters[0], true);

            // Make clicking the bg directly open the encounter
            GameObject.Find("ModBackground").GetComponent<Button>().onClick.RemoveAllListeners();
            GameObject.Find("ModBackground").GetComponent<Button>().onClick.AddListener(() => {
                if (animationDone) {
                    StaticInits.ENCOUNTER = encounters[0];
                    StartCoroutine(LaunchMod());
                }
                });
        } else {
            GameObject.Find("EncounterCount").GetComponent<Text>().text = "Has " + encounters.Count + " encounters";
            // crate your frisk version
            if (GlobalControls.crate)
                GameObject.Find("EncounterCount").GetComponent<Text>().text = "HSA " + encounters.Count + " ENCUOTNERS";
        }
        GameObject.Find("EncounterCountShadow").GetComponent<Text>().text = GameObject.Find("EncounterCount").GetComponent<Text>().text;

        // Update the color of the arrows
        if (CurrentSelectedMod == 0 && modDirs.Count == 1)
            GameObject.Find("BtnBack").transform.Find("Text").gameObject.GetComponent<Text>().color = new Color(0.25f, 0.25f, 0.25f, 1f);
        else
            GameObject.Find("BtnBack").transform.Find("Text").gameObject.GetComponent<Text>().color = new Color(1f, 1f, 1f, 1f);
        if (CurrentSelectedMod == modDirs.Count - 1 && modDirs.Count == 1)
            GameObject.Find("BtnNext").transform.Find("Text").gameObject.GetComponent<Text>().color = new Color(0.25f, 0.25f, 0.25f, 1f);
        else
            GameObject.Find("BtnNext").transform.Find("Text").gameObject.GetComponent<Text>().color = new Color(1f, 1f, 1f, 1f);
    }

    // Goes to the next or previous mod with a little scrolling animation.
    // -1 for left, 1 for right
    private void ScrollMods(int dir) {
        // First, determine if the next mod should be shown
        bool animate = false;
        //if ((dir == -1 && CurrentSelectedMod > 0) || (dir == 1 && CurrentSelectedMod < modDirs.Count - 1)) {
        if (modDirs.Count > 1)
            animate = true; //show the new mod

        // If the new mod is being shown, start the animation!
        if (animate) {
            animationTimer = dir / 10f;
            animationDone = false;

            // Create a BG for the previous mod
            GameObject OldBG = Instantiate(GameObject.Find("ModBackground"), GameObject.Find("Canvas").transform);
            OldBG.name = "ANIM ModBackground";
            OldBG.GetComponent<Button>().onClick.RemoveAllListeners();

            // Create a new mod title
            GameObject OldTitleShadow = Instantiate(GameObject.Find("ModTitleShadow"), GameObject.Find("Canvas").transform);
            OldTitleShadow.name = "ANIM ModTitleShadow";
            GameObject OldTitle = Instantiate(GameObject.Find("ModTitle"), GameObject.Find("Canvas").transform);
            OldTitle.name = "ANIM ModTitle";

            // Create a new encounter count label
            GameObject OldCountShadow = Instantiate(GameObject.Find("EncounterCountShadow"), GameObject.Find("Canvas").transform);
            OldCountShadow.name = "ANIM EncounterCountShadow";
            GameObject OldCount = Instantiate(GameObject.Find("EncounterCount"), GameObject.Find("Canvas").transform);
            OldCount.name = "ANIM EncounterCount";

            // Properly layer all "fake" assets
            OldCount.transform.SetAsFirstSibling();
            OldCountShadow.transform.SetAsFirstSibling();
            OldTitle.transform.SetAsFirstSibling();
            OldTitleShadow.transform.SetAsFirstSibling();
            OldBG.transform.SetAsFirstSibling();

            // Move all real assets to the side
            GameObject.Find("ModBackground").transform.Translate(640 * dir, 0, 0);
            GameObject.Find("ModTitleShadow").transform.Translate(640 * dir, 0, 0);
            GameObject.Find("ModTitle").transform.Translate(640 * dir, 0, 0);
            GameObject.Find("EncounterCountShadow").transform.Translate(640 * dir, 0, 0);
            GameObject.Find("EncounterCount").transform.Translate(640 * dir, 0, 0);

            // Actually choose the new mod
            CurrentSelectedMod = (CurrentSelectedMod + dir) % modDirs.Count;
            if (CurrentSelectedMod < 0) CurrentSelectedMod += modDirs.Count;

            ShowMod(CurrentSelectedMod);
        }
    }

    // Used to animate scrolling left or right.
    private void Update() {
        // Animation updating section
        if (GameObject.Find("ANIM ModBackground") != null) {
            if (animationTimer > 0)
                animationTimer = Mathf.Floor((animationTimer + 1));
            else
                animationTimer = Mathf.Ceil ((animationTimer - 1));

            int distance = (int)(((20 - Mathf.Abs(animationTimer)) * 3.4) * -Mathf.Sign(animationTimer));

            GameObject.Find("ANIM ModBackground")       .transform.Translate(distance, 0, 0);
            GameObject.Find("ANIM ModTitleShadow")      .transform.Translate(distance, 0, 0);
            GameObject.Find("ANIM ModTitle")            .transform.Translate(distance, 0, 0);
            GameObject.Find("ANIM EncounterCountShadow").transform.Translate(distance, 0, 0);
            GameObject.Find("ANIM EncounterCount")      .transform.Translate(distance, 0, 0);

            GameObject.Find("ModBackground")            .transform.Translate(distance, 0, 0);
            GameObject.Find("ModTitleShadow")           .transform.Translate(distance, 0, 0);
            GameObject.Find("ModTitle")                 .transform.Translate(distance, 0, 0);
            GameObject.Find("EncounterCountShadow")     .transform.Translate(distance, 0, 0);
            GameObject.Find("EncounterCount")           .transform.Translate(distance, 0, 0);

            if (Mathf.Abs(animationTimer) == 20) {
                Destroy(GameObject.Find("ANIM ModBackground"));
                Destroy(GameObject.Find("ANIM ModTitleShadow"));
                Destroy(GameObject.Find("ANIM ModTitle"));
                Destroy(GameObject.Find("ANIM EncounterCountShadow"));
                Destroy(GameObject.Find("ANIM EncounterCount"));

                // Manual movement because I can't change the movement multiplier to a precise enough value
                GameObject.Find("ModBackground")        .transform.Translate((int)(2 * -Mathf.Sign(animationTimer)), 0, 0);
                GameObject.Find("ModTitleShadow")       .transform.Translate((int)(2 * -Mathf.Sign(animationTimer)), 0, 0);
                GameObject.Find("ModTitle")             .transform.Translate((int)(2 * -Mathf.Sign(animationTimer)), 0, 0);
                GameObject.Find("EncounterCountShadow") .transform.Translate((int)(2 * -Mathf.Sign(animationTimer)), 0, 0);
                GameObject.Find("EncounterCount")       .transform.Translate((int)(2 * -Mathf.Sign(animationTimer)), 0, 0);

                animationTimer = 0;
                animationDone = true;
            }
        }

        // Prevent scrolling too far in the encounter box
        if (GameObject.Find("ScrollWin")) {
            GameObject content = encounterBox.transform.Find("ScrollCutoff/Content").gameObject;
            if (content.GetComponent<RectTransform>().anchoredPosition.y < -200)
                content.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -200);
            else if (content.GetComponent<RectTransform>().anchoredPosition.y > (content.transform.childCount - 1) * 30)
                content.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, (content.transform.childCount - 1) * 30);
        }

        // Detect hovering over the Exit button and handle fading
        if ((ScreenResolution.mousePosition.x / ScreenResolution.displayedSize.x) * 640 < 70 && (Input.mousePosition.y / ScreenResolution.displayedSize.y) * 480 > 450 && ExitButtonAlpha < 1f) {
            ExitButtonAlpha += 0.05f;
            GameObject.Find("BtnExit/Text").GetComponent<Text>().color =        new Color(1f, 1f, 1f, ExitButtonAlpha);
            GameObject.Find("BtnExit/TextShadow").GetComponent<Text>().color =  new Color(0f, 0f, 0f, ExitButtonAlpha);
        } else if (ExitButtonAlpha > 0.5f) {
            ExitButtonAlpha -= 0.05f;
            GameObject.Find("BtnExit/Text").GetComponent<Text>().color =        new Color(1f, 1f, 1f, ExitButtonAlpha);
            GameObject.Find("BtnExit/TextShadow").GetComponent<Text>().color =  new Color(0f, 0f, 0f, ExitButtonAlpha);
        }

        // Detect hovering over the Options button and handle fading
        if (GlobalControls.modDev) {
            if ((ScreenResolution.mousePosition.x / ScreenResolution.displayedSize.x) * 640 > 550 && (Input.mousePosition.y / ScreenResolution.displayedSize.y) * 480 > 450 && OptionsButtonAlpha < 1f) {
                OptionsButtonAlpha += 0.05f;
                GameObject.Find("BtnOptions/Text").GetComponent<Text>().color =        new Color(1f, 1f, 1f, OptionsButtonAlpha);
                GameObject.Find("BtnOptions/TextShadow").GetComponent<Text>().color =  new Color(0f, 0f, 0f, OptionsButtonAlpha);
            } else if (OptionsButtonAlpha > 0.5f) {
                OptionsButtonAlpha -= 0.05f;
                GameObject.Find("BtnOptions/Text").GetComponent<Text>().color =        new Color(1f, 1f, 1f, OptionsButtonAlpha);
                GameObject.Find("BtnOptions/TextShadow").GetComponent<Text>().color =  new Color(0f, 0f, 0f, OptionsButtonAlpha);
            }
        }

        // Controls:

        ////////////////// Main: ////////////////////////////////////
        //    Z or Return: Start encounter (if mod has only one    //
        //                 encounter), or open encounter list      //
        //     Shift or X: Return to Disclaimer screen             //
        //        Up or C: Open the mod list                       //
        //           Left: Scroll left                             //
        //          Right: Scroll right                            //
        ////////////////// Encounter or Mod list: ///////////////////
        //    Z or Return: Start an encounter, or select a mod     //
        //     Shift or X: Exit                                    //
        //             Up: Move up                                 //
        //           Down: Move down                               //
        /////////////////////////////////////////////////////////////

        // Main controls:
        if (!GameObject.Find("ScrollWin")) {
            if (animationDone) {
                //scroll left
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                    ScrollMods(-1);
                //scroll right
                else if (Input.GetKeyDown(KeyCode.RightArrow))
                    ScrollMods(1);
                //open the mod list
                else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.C)) {
                    modFolderMiniMenu();
                    encounterBox.transform.Find("ScrollCutoff/Content").GetChild(selectedItem).GetComponent<MenuButton>().StartAnimation(1);
                // Open the encounter list or start the encounter (if there is only one encounter)
                } else if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return))
                    GameObject.Find("ModBackground").GetComponent<Button>().onClick.Invoke();
                    //encounterBox.transform.Find("ScrollCutoff/Content").GetChild(selectedItem).GetComponent<MenuButton>().StartAnimation(1);
            }

            // Return to the Disclaimer screen
            if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
                GameObject.Find("BtnExit").GetComponent<Button>().onClick.Invoke();
        // Encounter or Mod List controls:
        } else {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)) {
                GameObject content = encounterBox.transform.Find("ScrollCutoff/Content").gameObject;

                // Store previous value of selectedItem
                int previousSelectedItem = selectedItem;

                //move up
                if (Input.GetKeyDown(KeyCode.UpArrow))
                    selectedItem -= 1;
                //move down
                else if (Input.GetKeyDown(KeyCode.DownArrow))
                    selectedItem += 1;

                // Keep the selector in-bounds!
                if (selectedItem < 0)
                    selectedItem = content.transform.childCount - 1;
                else if (selectedItem > content.transform.childCount - 1)
                    selectedItem = 0;

                // Update the buttons!

                // Animate the old button
                GameObject previousButton = content.transform.GetChild(previousSelectedItem).gameObject;
                previousButton.GetComponent<MenuButton>().StartAnimation(-1);
                //previousButton.spriteState = SpriteState.

                // Animate the new button
                GameObject newButton = content.transform.GetChild(selectedItem).gameObject;
                newButton.GetComponent<MenuButton>().StartAnimation(1);

                // Scroll to the newly chosen button if it is hidden!
                float buttonTopEdge    = -newButton.GetComponent<RectTransform>().anchoredPosition.y + 100;
                float buttonBottomEdge = -newButton.GetComponent<RectTransform>().anchoredPosition.y + 100 + 30;

                float topEdge    = content.GetComponent<RectTransform>().anchoredPosition.y;
                float bottomEdge = content.GetComponent<RectTransform>().anchoredPosition.y + 230;

                //button is above the top of the scrolly bit
                if      (topEdge    > buttonTopEdge)
                    content.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, buttonTopEdge);
                //button is below the bottom of the scrolly bit
                else if (bottomEdge < buttonBottomEdge)
                    content.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, buttonBottomEdge - 230);
            }

            // Exit
            if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
                GameObject.Find("ModBackground").GetComponent<Button>().onClick.Invoke();
            // Select the mod or encounter
            else if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return))
                encounterBox.transform.Find("ScrollCutoff/Content").GetChild(selectedItem).gameObject.GetComponent<Button>().onClick.Invoke();
        }
    }

    // Shows the "mod page" screen.
    private void modFolderSelection() {
        UnitaleUtil.printDebuggerBeforeInit = "";
        ShowMod(CurrentSelectedMod);

        //hide the 4 buttons if needed
        if (!GlobalControls.modDev)
            devMod.SetActive(false);

        //show the mod list button
        btnList.SetActive(true);

        // If the encounter box is visible, remove all encounter buttons before hiding
        // Use try because GameObject.Find will not find inactive GameObjects
        try {
            Transform content = encounterBox.transform.Find("ScrollCutoff/Content");
            foreach (Transform b in content) {
                if (b.gameObject.name != "Back")
                    Destroy(b.gameObject);
                else
                    b.GetComponent<MenuButton>().Reset();
            }
        } catch {}
        //hide the encounter selection box
        encounterBox.SetActive(false);
    }

    // Shows the list of available encounters in a mod.
    private void encounterSelection() {
        //hide the mod list button
        btnList.SetActive(false);

        //automatically choose "back"
        selectedItem = 0;

        // Make clicking the background exit the encounter selection screen
        GameObject.Find("ModBackground").GetComponent<Button>().onClick.RemoveAllListeners();
        GameObject.Find("ModBackground").GetComponent<Button>().onClick.AddListener(() => {
            if (animationDone)
                modFolderSelection();
            });
        //show the encounter selection box
        encounterBox.SetActive(true);
        //reset the encounter box's position
        encounterBox.transform.Find("ScrollCutoff/Content").GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);

        //grab pre-existing objects
        GameObject content = encounterBox.transform.Find("ScrollCutoff/Content").gameObject;
        //give the back button its function
        GameObject back = content.transform.Find("Back").gameObject;
        back.GetComponent<Button>().onClick.RemoveAllListeners();
        back.GetComponent<Button>().onClick.AddListener(() => {modFolderSelection();});

        DirectoryInfo di = new DirectoryInfo(Path.Combine(FileLoader.DataRoot, "Mods/" + StaticInits.MODFOLDER + "/Lua/Encounters"));
        if (di.Exists && di.GetFiles().Length > 0) {
            FileInfo[] encounterFiles = di.GetFiles("*.lua");

            int count = 0;
            foreach (FileInfo encounter in encounterFiles) {
                count += 1;

                //create a button for each encounter file
                GameObject button = Instantiate(back);

                //set parent and name
                button.transform.SetParent(content.transform);
                button.name = "EncounterButton";

                //set position
                button.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 100 - (count * 30));

                //set color
                button.GetComponent<Image>().color = new Color(0.75f, 0.75f, 0.75f, 0.5f);
                button.GetComponent<MenuButton>().NormalColor = new Color(0.75f, 0.75f, 0.75f, 0.5f);
                button.GetComponent<MenuButton>().HoverColor  = new Color(0.75f, 0.75f, 0.75f, 1f);
                button.transform.Find("Fill").GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);

                // set text
                button.transform.Find("Text").GetComponent<Text>().text = Path.GetFileNameWithoutExtension(encounter.Name);
                if (GlobalControls.crate)
                    button.transform.Find("Text").GetComponent<Text>().text = Temmify.Convert(Path.GetFileNameWithoutExtension(encounter.Name), true);

                //finally, set function!
                string filename = Path.GetFileNameWithoutExtension(encounter.Name);

                int tempCount = count;

                button.GetComponent<Button>().onClick.RemoveAllListeners();
                button.GetComponent<Button>().onClick.AddListener(() => {
                    selectedItem = tempCount;
                    StaticInits.ENCOUNTER = filename;
                    StartCoroutine(LaunchMod());
                });
            }
        }
    }

    // Opens the scrolling interface and lets the user browse their mods.
    private void modFolderMiniMenu() {
        // Hide the mod list button
        btnList.SetActive(false);

        // Automatically select the current mod when the mod list appears
        selectedItem = CurrentSelectedMod + 1;

        // Grab pre-existing objects
        GameObject content = encounterBox.transform.Find("ScrollCutoff/Content").gameObject;
        // Give the back button its function
        GameObject back = content.transform.Find("Back").gameObject;
        back.GetComponent<Button>().onClick.RemoveAllListeners();
        back.GetComponent<Button>().onClick.AddListener(() => {
            // Reset the encounter box's position
            modListScroll = 0.0f;
            modFolderSelection();
            });

        // Make clicking the background exit this menu
        GameObject.Find("ModBackground").GetComponent<Button>().onClick.RemoveAllListeners();
        GameObject.Find("ModBackground").GetComponent<Button>().onClick.AddListener(() => {
            if (animationDone) {
                // Store the encounter box's position so it can be remembered upon exiting a mod
                modListScroll = content.GetComponent<RectTransform>().anchoredPosition.y;
                modFolderSelection();
            }
            });
        // Show the encounter selection box
        encounterBox.SetActive(true);
        // Move the encounter box to the stored position, for easier mod browsing
        content.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, modListScroll);

        int count = -1;
        foreach (DirectoryInfo mod in modDirs) {
            count += 1;

            // Create a button for each mod
            GameObject button = Instantiate(back);

            //set parent and name
            button.transform.SetParent(content.transform);
            button.name = "ModButton";

            //set position
            button.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 100 - ((count + 1) * 30));

            //set color
            button.GetComponent<Image>().color = new Color(0.75f, 0.75f, 0.75f, 0.5f);
            button.GetComponent<MenuButton>().NormalColor = new Color(0.75f, 0.75f, 0.75f, 0.5f);
            button.GetComponent<MenuButton>().HoverColor  = new Color(0.75f, 0.75f, 0.75f, 1f);
            button.transform.Find("Fill").GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);

            //set text
            button.transform.Find("Text").GetComponent<Text>().text = mod.Name;
            if (GlobalControls.crate)
                button.transform.Find("Text").GetComponent<Text>().text = Temmify.Convert(mod.Name, true);

            //finally, set function!
            int tempCount = count;

            button.GetComponent<Button>().onClick.RemoveAllListeners();
            button.GetComponent<Button>().onClick.AddListener(() => {
                // Store the encounter box's position so it can be remembered upon exiting a mod
                modListScroll = content.GetComponent<RectTransform>().anchoredPosition.y;

                CurrentSelectedMod = tempCount;
                modFolderSelection();
                ShowMod(CurrentSelectedMod);
            });
        }
    }
}
