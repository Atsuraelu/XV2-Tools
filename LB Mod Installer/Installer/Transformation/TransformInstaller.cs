﻿using System;
using System.Collections.Generic;
using System.Linq;
using Xv2CoreLib.Eternity;
using LB_Mod_Installer.Binding;
using Xv2CoreLib.CMS;
using Xv2CoreLib.CUS;
using Xv2CoreLib.BAC;
using Xv2CoreLib.Resource;
using Xv2CoreLib.BCM;
using Xv2CoreLib.PUP;
using Xv2CoreLib.IDB;
using Xv2CoreLib.BCS;
using YAXLib;

namespace LB_Mod_Installer.Installer.Transformation
{
    public class TransformInstaller
    {
        private Install install;

        private List<TransformDefine> TransformationDefines = new List<TransformDefine>();
        private List<TransformPartSet> PartSets = new List<TransformPartSet>();
        private List<TransformPowerUp> PupEntries = new List<TransformPowerUp>();
        private List<TransformCusAura> CusAuras = new List<TransformCusAura>();

        public TransformInstaller(Install install)
        {
            this.install = install;
        }

        #region Public
        public void LoadTransformations(TransformDefines defines)
        {
            if(defines?.Transformations != null)
            {
                foreach (var define in defines.Transformations)
                {
                    BAC_File bacDefines = install.zipManager.DeserializeXmlFromArchive_Ext<BAC_File>(GeneralInfo.GetPathInZipDataDir(define.BacPath));
                    define.BacEntryInstance = bacDefines.GetEntry(define.BacEntry);

                    if (define.BacEntryInstance == null)
                        throw new Exception($"TransformInstaller.LoadTransformations: BacEntry with ID: {define.BacEntry} was not found for Key: {define.Key}, but the bac file was loaded.\n\nCould be a misconfigured bac file, or a wrong ID/BacPath?");

                    var existing = TransformationDefines.FirstOrDefault(x => x.Key == define.Key);

                    if(existing != null)
                    {
                        TransformationDefines[TransformationDefines.IndexOf(existing)] = define;
                    }
                    else
                    {
                        TransformationDefines.Add(define);
                    }
                }
            }
        }
    
        public void LoadPartSets(TransformPartSets partSets)
        {
            if(partSets?.PartSets != null)
            {
                foreach(var partSet in partSets.PartSets)
                {
                    var existing = PartSets.FirstOrDefault(x => x.Key == partSet.Key && x.Race == partSet.Race && x.Gender == partSet.Gender);

                    if (existing != null)
                    {
                        PartSets[PartSets.IndexOf(existing)] = partSet;
                    }
                    else
                    {
                        PartSets.Add(partSet);
                    }
                }
            }
        }
        
        public void LoadPupEntries(TransformPowerUps powerUps)
        {
            if (powerUps?.PowerUps != null)
            {
                foreach (var powerUp in powerUps.PowerUps)
                {
                    var existing = PupEntries.FirstOrDefault(x => x.Key == powerUp.Key);

                    if (existing != null)
                    {
                        PupEntries[PupEntries.IndexOf(existing)] = powerUp;
                    }
                    else
                    {
                        PupEntries.Add(powerUp);
                    }
                }
            }
        }

        public void LoadCusAuras(TransformCusAuras cusAuras)
        {
            if (cusAuras?.CusAuras != null)
            {
                foreach (var cusAura in cusAuras.CusAuras)
                {
                    var existing = CusAuras.FirstOrDefault(x => x.Key == cusAura.Key);

                    if (existing != null)
                    {
                        CusAuras[CusAuras.IndexOf(existing)] = cusAura;
                    }
                    else
                    {
                        CusAuras.Add(cusAura);
                    }
                }
            }
        }

        public void InstallSkill(TransformSkill skill)
        {
            BAC_File bacFile = CreateBacFile(skill);
            BCM_File bcmFile = CreateBcmFile(skill);

            //EEPK, EAN, ACB - all statically linked. Just put the path in cus.

            //Assign ID (generate dummy cms if needed)
            CUS_File cusFile = (CUS_File)install.GetParsedFile<CUS_File>(BindingManager.CUS_PATH);
            CMS_Entry dummyCms = AssignCmsEntry();
            int skillID2 = cusFile.AssignNewSkillId(dummyCms, CUS_File.SkillType.Awoken);
            int skillID1 = skillID2 + 25000;

            if (skillID1 == -1)
                throw new ArgumentOutOfRangeException($"TransformInstaller.InstallSkill: the assigned skill ID is invalid (shouldn't happen... so something went wrong somewhere else)");

            bacFile.ChangeNeutralSkillId((ushort)skillID2);

            //Create PUP entries
            int pupID = InstallPupEntries(skill);

            //Create skill idb entry
            InstallIdbEntry(skill, skillID2);

            //Create CusAura entries
            if (skill.CusAura == -1)
                skill.CusAura = InstallCusAuras(skill);

            //Create PartSets
            if (skill.PartSet == -1)
                skill.PartSet = InstallPartSets(skill);

            //Create CUS entry
            Skill cusEntry = new Skill();
            cusEntry.ShortName = skill.ThreeLetterCode;
            cusEntry.ID1 = (ushort)skillID1;
            cusEntry.ID2 = (ushort)skillID2;
            cusEntry.I_12 = skill.RaceLock;
            cusEntry.I_13 = 0x76; //BAC, BCM, EAN, Awoken Skill
            cusEntry.FilesLoadedFlags1 = Skill.FilesLoadedFlags.Eepk | Skill.FilesLoadedFlags.CamEan | Skill.FilesLoadedFlags.CharaVOX | Skill.FilesLoadedFlags.CharaSE;
            cusEntry.I_16 = (short)skill.PartSet;
            cusEntry.I_18 = 65280;
            cusEntry.EanPath = skill.EanPath;
            cusEntry.CamEanPath = skill.CamEanPath;
            cusEntry.EepkPath = skill.VfxPath;
            cusEntry.SePath = skill.SeAcbPath;
            cusEntry.VoxPath = skill.VoxAcbPath;
            cusEntry.I_50 = 271;
            cusEntry.I_52 = 3;
            cusEntry.I_54 = 2;
            cusEntry.PUP = (ushort)pupID;
            cusEntry.CusAura = (short)skill.CusAura;
            cusEntry.CharaSwapId = (ushort)skill.CharaSwapId;
            cusEntry.I_62 = (short)skill.GetSkillSetChangeId();
            cusEntry.NumTransformations = (ushort)(skill.NumStages > 3 ? skill.NumStages : 4); //Always set this to atleast 4 so that the stage names breaks and only shows the first stage (otherwise, we get "Unknown Skill" unless more msg entries are added)

            cusFile.AwokenSkills.Add(cusEntry);
            GeneralInfo.Tracker.AddID(BindingManager.CUS_PATH, Sections.CUS_AwokenSkills, cusEntry.Index);

            //Save files (add to file cache)
            string folderName = $"{skillID2.ToString("D3")}_{dummyCms.ShortName}_{skill.ThreeLetterCode}";
            string bacPath = $"skill/MET/{folderName}/{folderName}.bac";
            string bcmPath = $"skill/MET/{folderName}/{folderName}_PLAYER.bcm";

            install.fileManager.AddParsedFile(bacPath, bacFile);
            install.fileManager.AddParsedFile(bcmPath, bcmFile);

            GeneralInfo.Tracker.AddJungleFile(bacPath);
            GeneralInfo.Tracker.AddJungleFile(bcmPath);
        }
        #endregion

        #region Install
        private BAC_File CreateBacFile(TransformSkill skill)
        {
            BAC_File bacFile = BAC_File.DefaultBacFile();
            
            foreach(var stage in skill.Stages)
            {
                var defineEntry = TransformationDefines.FirstOrDefault(x => x.Key?.Equals(stage.Key, StringComparison.OrdinalIgnoreCase) == true);

                if (defineEntry == null)
                    throw new Exception($"TransformInstaller.CreateBacFile: Cannot find the BacFile with Key: {stage.Key}");

                if (bacFile.GetEntry(stage.StageIndex) != null)
                    throw new Exception($"TransformInstaller.CreateBacFile: Duplicate StageIndex encountered.");

                var entry = defineEntry.BacEntryInstance.Copy();
                entry.SortID = stage.StageIndex;

                bacFile.BacEntries.Add(entry);
            }

            //Add transform / revert mechanic entries
            var holdDownEntryDefine = TransformationDefines.FirstOrDefault(x => x.Key?.Equals(TransformDefine.BAC_HOLD_DOWN_LOOP_KEY) == true);
            var untransformDefine = TransformationDefines.FirstOrDefault(x => x.Key?.Equals(TransformDefine.BAC_UNTRANSFORM_KEY) == true);
            var revertDefine = TransformationDefines.FirstOrDefault(x => x.Key?.Equals(TransformDefine.BAC_REVERT_LOOP_KEY) == true);
            var callbackDefine = TransformationDefines.FirstOrDefault(x => x.Key?.Equals(TransformDefine.BAC_CALLBACK_KEY) == true);

            if(holdDownEntryDefine == null)
                throw new Exception(string.Format("TransformInstaller.CreateBacFile: Cannot find the define entry for \"{0}\"", TransformDefine.BAC_HOLD_DOWN_LOOP_KEY));

            if (untransformDefine == null)
                throw new Exception(string.Format("TransformInstaller.CreateBacFile: Cannot find the define entry for \"{0}\"", TransformDefine.BAC_UNTRANSFORM_KEY));

            if (revertDefine == null)
                throw new Exception(string.Format("TransformInstaller.CreateBacFile: Cannot find the define entry for \"{0}\"", TransformDefine.BAC_REVERT_LOOP_KEY));

            if (callbackDefine == null)
                throw new Exception(string.Format("TransformInstaller.CreateBacFile: Cannot find the define entry for \"{0}\"", TransformDefine.BAC_CALLBACK_KEY));

            var holdDownEntry = holdDownEntryDefine.BacEntryInstance.Copy();
            var untransformEntry = untransformDefine.BacEntryInstance.Copy();
            var revertEntry = revertDefine.BacEntryInstance.Copy();
            var callbackEntry = callbackDefine.BacEntryInstance.Copy();

            holdDownEntry.SortID = TransformDefine.BAC_HOLD_DOWN_LOOP_IDX;
            untransformEntry.SortID = TransformDefine.BAC_UNTRANSFORM_IDX;
            revertEntry.SortID = TransformDefine.BAC_REVERT_IDX;
            callbackEntry.SortID = TransformDefine.BAC_CALLBACK_IDX;

            bacFile.BacEntries.Add(holdDownEntry);
            bacFile.BacEntries.Add(untransformEntry);
            bacFile.BacEntries.Add(revertEntry);
            bacFile.BacEntries.Add(callbackEntry);

            return bacFile;
        }

        private BCM_File CreateBcmFile(TransformSkill skill)
        {
            BCM_File bcmFile = new BCM_File();

            //Add root entry
            BCM_Entry root = new BCM_Entry();
            bcmFile.BCMEntries.Add(root);

            //Add "Hold Down" entries
            for(int i = 0; i < skill.TransformStates.Count; i++)
            {
                BCM_Entry holdLoop = new BCM_Entry();
                holdLoop.ButtonInput = ButtonInput.ultimateskill2;
                holdLoop.ActivatorState = ActivatorState.attacking | ActivatorState.idle;
                holdLoop.BacCase = BacCases.Case3;
                holdLoop.PrimaryActivatorConditions = (uint)(i == 0 ? 0x80 : 0xd00);
                holdLoop.BacEntryPrimary = TransformDefine.BAC_HOLD_DOWN_LOOP_IDX;
                holdLoop.TransStage = (short)(skill.GetTransStage(i));
                holdLoop.KiRequired = skill.TransformStates[i].KiRequired;
                holdLoop.HealthRequired = skill.TransformStates[i].HealthRequired;

                //Add Transform entries
                if(skill.TransformStates[i].TransformOptions != null)
                {
                    if (skill.TransformStates[i].TransformOptions.Count >= 4)
                        throw new ArgumentException($"TransformInstaller.CreateBcmFile: Number of TransformOption entries ({skill.TransformStates[i].TransformOptions.Count}) exceeds the maximum of 4. ");

                    for (int a = 0; a < skill.TransformStates[i].TransformOptions.Count; a++)
                    {
                        BCM_Entry transformEntry = new BCM_Entry();

                        transformEntry.ButtonInput = GetButtonInputForSlot(a);
                        transformEntry.ActivatorState = ActivatorState.attacking | ActivatorState.idle;
                        transformEntry.BacCase = BacCases.Case3;
                        transformEntry.BacEntryPrimary = (short)skill.TransformStates[i].TransformOptions[a].StageIndex;
                        transformEntry.TransStage = (short)skill.GetTransStage(skill.TransformStates[i].TransformOptions[a].StageIndex);
                        transformEntry.KiRequired = skill.TransformStates[i].TransformOptions[a].KiRequired;
                        transformEntry.HealthRequired = skill.TransformStates[i].TransformOptions[a].HealthRequired;

                        holdLoop.BCMEntries.Add(transformEntry);
                    }
                }

                //Add untransform entry (when not on the 1st stage selector / is in base form)
                if(i > 0)
                {
                    BCM_Entry untransformRoot = new BCM_Entry();
                    untransformRoot.ButtonInput = ButtonInput.guard;
                    untransformRoot.ActivatorState = ActivatorState.attacking | ActivatorState.idle;
                    untransformRoot.BacCase = BacCases.Case3;
                    untransformRoot.BacEntryPrimary = TransformDefine.BAC_UNTRANSFORM_IDX;

                    //Add revert entries
                    if (skill.TransformStates[i].RevertOptions != null)
                    {
                        if (skill.TransformStates[i].RevertOptions.Count >= 4)
                            throw new ArgumentException($"TransformInstaller.CreateBcmFile: Number of RevertOptions entries ({skill.TransformStates[i].RevertOptions.Count}) exceeds the maximum of 4. ");

                        for (int a = 0; a < skill.TransformStates[i].RevertOptions.Count; a++)
                        {
                            BCM_Entry revertEntry = new BCM_Entry();
                            revertEntry.ButtonInput = GetButtonInputForSlot(a);
                            revertEntry.ActivatorState = ActivatorState.attacking | ActivatorState.idle;
                            revertEntry.BacCase = BacCases.Case3;
                            revertEntry.BacEntryPrimary = (short)skill.TransformStates[i].RevertOptions[a].StageIndex;
                            revertEntry.TransStage = (short)skill.GetTransStage(skill.TransformStates[i].RevertOptions[a].StageIndex);

                            untransformRoot.BCMEntries.Add(revertEntry);
                        }
                    }

                    holdLoop.BCMEntries.Add(untransformRoot);
                }

                root.BCMEntries.Add(holdLoop);
            }

            return bcmFile;
        }

        private CMS_Entry AssignCmsEntry()
        {
            CMS_File cmsFile = (CMS_File)install.GetParsedFile<CMS_File>(BindingManager.CMS_PATH);
            CUS_File cusFile = (CUS_File)install.GetParsedFile<CUS_File>(BindingManager.CUS_PATH);

            //Find dummy CMS entry
            CMS_Entry dummyCmsEntry = null;

            foreach(var cmsEntry in cmsFile.CMS_Entries)
            {
                if (cmsEntry.IsDummyEntry())
                {
                    if(!cusFile.IsSkillIdRangeUsed(cmsEntry, CUS_File.SkillType.Awoken))
                    {
                        dummyCmsEntry = cmsEntry;
                        break;
                    }
                }
            }

            //If no suitable dummy was found, create a new one
            if(dummyCmsEntry == null)
            {
                dummyCmsEntry = cmsFile.CreateDummyEntry();
                cmsFile.CMS_Entries.Add(dummyCmsEntry);
            }

            return dummyCmsEntry;
        }
        
        private int InstallPupEntries(TransformSkill skill)
        {
            PUP_File pupFile = (PUP_File)install.GetParsedFile<PUP_File>(Xv2CoreLib.Xenoverse2.PUP_PATH);
            int pupID = Install.bindingManager.GetAutoId(null, pupFile.PupEntries, 350, ushort.MaxValue, skill.NumStages);

            //Dummy entry for stage 0 
            if(skill.HasMoveSkillSetChange())
                pupFile.PupEntries.Add(new PUP_Entry(pupID));

            for (int i = 0; i < skill.Stages.Count; i++)
            {
                var pup = PupEntries.FirstOrDefault(x => x.Key == skill.Stages[i].Key);

                if (pup == null)
                    throw new Exception($"Could not find a PUP entry for Key: {skill.Stages[i].Key}.\n\nNote: All transformations must have an assigned PUP entry!");

                var pupEntry = pup.PupEntry.Copy();
                pupEntry.ID = pupID + skill.GetTransStage(i);

                pupFile.PupEntries.Add(pupEntry);
                GeneralInfo.Tracker.AddID(Xv2CoreLib.Xenoverse2.PUP_PATH, Sections.PUP_Entry, pupEntry.Index);
            }

            return pupID;
        }

        private int InstallCusAuras(TransformSkill skill)
        {
            PrebakedFile prebaked = (PrebakedFile)install.GetParsedFile<PrebakedFile>(PrebakedFile.PATH);
            int auraId = prebaked.GetFreeCusAuraID(skill.NumStages);

            //Dummy entry for stage 0 
            if(skill.HasMoveSkillSetChange())
                prebaked.CusAuras.Add(new CusAuraData(auraId));

            for (int i = 0; i < skill.Stages.Count; i++)
            {
                var cusAura = CusAuras.FirstOrDefault(x => x.Key == skill.Stages[i].Key);

                if (cusAura == null)
                    throw new Exception($"Could not find a CusAuraData entry for Key: {skill.Stages[i].Key}.");

                var entry = cusAura.CusAuraData.Copy();
                entry.CusAuraID = (ushort)(auraId + skill.GetTransStage(skill.Stages[i].StageIndex));

                prebaked.CusAuras.Add(entry);
                GeneralInfo.Tracker.AddID(PrebakedFile.PATH, Sections.PrebakedCusAura, entry.CusAuraID.ToString());
            }

            return auraId;
        }

        private int InstallPartSets(TransformSkill skill)
        {
            int id = Install.bindingManager.GetFreePartSet(1000, ushort.MaxValue, skill.NumStages);
            int movesetChangeIdx = skill.IndexOfMoveSkillSetChange();

            List<BCS_File> bcsFiles = new List<BCS_File>();
            List<PartSet> dummyPartSets = new List<PartSet>();

            for(int i = 0; i < skill.Stages.Count; i++)
            {
                foreach (var partSet in PartSets.Where(x => x.Key == skill.Stages[i].Key))
                {
                    string path = BCS_File.GetBcsFilePath(partSet.Race, partSet.Gender);
                    BCS_File bcsFile = (BCS_File)install.GetParsedFile<BCS_File>(path);

                    PartSet newPartSet = partSet.PartSet.Copy();
                    newPartSet.ID = id + skill.GetTransStage(skill.Stages[i].StageIndex);
                    bcsFile.PartSets.Add(newPartSet);

                    GeneralInfo.Tracker.AddID(path, Sections.BCS_PartSets, newPartSet.ID.ToString());

                    //Save bcs file for adding dummy stage 0 entry later
                    if (movesetChangeIdx == i)
                    {
                        if (!bcsFiles.Contains(bcsFile))
                        {
                            bcsFiles.Add(bcsFile);
                            dummyPartSets.Add(newPartSet);
                        }
                    }
                }
            }

            //Add entry at 0 if the skill has a moveset or skillset change, using the PartSet of the stage where the change should occur
            if (skill.HasMoveSkillSetChange())
            {
                for(int i = 0; i < bcsFiles.Count; i++)
                {
                    PartSet newPartSet = dummyPartSets[i].Copy();
                    newPartSet.ID = id;

                    bcsFiles[i].PartSets.Add(newPartSet);

                    GeneralInfo.Tracker.AddID(BCS_File.GetBcsFilePath(bcsFiles[i].Race, bcsFiles[i].Gender), Sections.BCS_PartSets, id.ToString());
                }
            }

            return id;
        }

        private void InstallIdbEntry(TransformSkill skill, int skillID2)
        {
            //Create IDB entry
            IDB_File idbFile = (IDB_File)install.GetParsedFile<IDB_File>(Xv2CoreLib.Xenoverse2.SKILL_IDB_PATH);
            IDB_Entry idbEntry = IDB_Entry.GetDefaultSkillEntry(skillID2, 5, skill.GetMaxKiRequired());

            idbFile.Entries.Add(idbEntry);
            GeneralInfo.Tracker.AddID(Xv2CoreLib.Xenoverse2.SKILL_IDB_PATH, Sections.IDB_Entries, idbEntry.Index);

            //Create MSG entries
            string[] names = install.installerXml.GetLocalisedArray(skill.Name);
            string[] descs = install.installerXml.GetLocalisedArray(skill.Info);

            install.msgComponentInstall.WriteSkillMsgEntries(names, skillID2, CUS_File.SkillType.Awoken, MsgComponentInstall.SkillMode.Name);
            install.msgComponentInstall.WriteSkillMsgEntries(descs, skillID2, CUS_File.SkillType.Awoken, MsgComponentInstall.SkillMode.Info);
            install.msgComponentInstall.WriteSkillMsgEntries(names, skillID2, CUS_File.SkillType.Awoken, MsgComponentInstall.SkillMode.BtlHud);
        }

        #endregion

        private ButtonInput GetButtonInputForSlot(int slot)
        {
            switch (slot)
            {
                case 0:
                    return ButtonInput.awokenskill;
                case 1:
                    return ButtonInput.ultimateskill2;
                case 2:
                    return ButtonInput.ultimateskill1;
                case 3:
                    return ButtonInput.superskill1;
            }

            throw new ArgumentException($"TransformInstaller.GetButtonInputForSlot: Slot number out of range ({slot}), must be between 0 and 3.");
        }
        
        public static void CreateDummyXmls()
        {
            TransformCusAuras cusAuras = new TransformCusAuras();
            cusAuras.CusAuras = new List<TransformCusAura>();
            cusAuras.CusAuras.Add(new TransformCusAura());
            cusAuras.CusAuras[0].CusAuraData = new CusAuraData();

            TransformDefines transformDefines = new TransformDefines();
            transformDefines.Transformations = new List<TransformDefine>();
            transformDefines.Transformations.Add(new TransformDefine());

            TransformPartSets partSets = new TransformPartSets();
            partSets.PartSets = new List<TransformPartSet>();
            partSets.PartSets.Add(new TransformPartSet());
            partSets.PartSets[0].PartSet = new PartSet();

            TransformPowerUps powerUps = new TransformPowerUps();
            powerUps.PowerUps = new List<TransformPowerUp>();
            powerUps.PowerUps.Add(new TransformPowerUp());
            powerUps.PowerUps[0].PupEntry = new PUP_Entry();

            TransformSkill skill = new TransformSkill();
            skill.TransformStates = new List<TransformState>();
            skill.Stages = new List<TransformStage>();
            skill.Stages.Add(new TransformStage());
            skill.TransformStates.Add(new TransformState());
            skill.TransformStates[0].TransformOptions = new List<TransformOption>();
            skill.TransformStates[0].RevertOptions = new List<TransformOption>();
            skill.TransformStates[0].TransformOptions.Add(new TransformOption());
            skill.TransformStates[0].RevertOptions.Add(new TransformOption());

            System.IO.Directory.CreateDirectory("transform");

            YAXSerializer serializer = new YAXSerializer(typeof(TransformCusAuras));
            serializer.SerializeToFile(cusAuras, "transform/CusAuras_CusAuraDefine.xml");

            YAXSerializer serializer2 = new YAXSerializer(typeof(TransformDefines));
            serializer2.SerializeToFile(transformDefines, "transform/Transform_TransformDefine.xml");

            YAXSerializer serializer3 = new YAXSerializer(typeof(TransformPartSets));
            serializer3.SerializeToFile(partSets, "transform/PartSets_PartSetDefine.xml");

            YAXSerializer serializer4 = new YAXSerializer(typeof(TransformPowerUps));
            serializer4.SerializeToFile(powerUps, "transform/PUP_PowerUpDefine.xml");

            YAXSerializer serializer5 = new YAXSerializer(typeof(TransformSkill));
            serializer5.SerializeToFile(skill, "transform/Skill_TransformSkill.xml");

        }
    }
}
