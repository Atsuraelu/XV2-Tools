﻿using LB_Mod_Installer.Installer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xv2CoreLib;
using Xv2CoreLib.AUR;
using Xv2CoreLib.BCS;
using Xv2CoreLib.CMS;
using Xv2CoreLib.CSO;
using Xv2CoreLib.CUS;
using Xv2CoreLib.Eternity;
using Xv2CoreLib.HCI;
using Xv2CoreLib.PSC;
using Xv2CoreLib.TTB;
using YAXLib;

namespace LB_Mod_Installer.Binding
{
    public class BindingManager
    {
        public const int NullTokenInt = 1280070990;
        public const string NullTokenStr = "1280070990";
        private const int SkipToken = 32532;
        private const string SkipTokenStr = "32532";

        public const string CUS_PATH = "system/custom_skill.cus";
        public const string CMS_PATH = "system/char_model_spec.cms";
        public const string CSO_PATH = "system/chara_sound.cso";
        public const string PSC_PATH = "system/parameter_spec_char.psc";
        public const string AUR_PATH = "system/aura_setting.aur";
        public const string TTB_PATH = "quest/XTALK/CommonDialogue.ttb";
        private const string HCI_PATH = "ui/CharaImage/chara_image.hci";
        private const string HUM_BCS_PATH = "chara/HUM/HUM.bcs";
        private const string HUF_BCS_PATH = "chara/HUF/HUF.bcs";
        private const string MAM_BCS_PATH = "chara/MAM/MAM.bcs";
        private const string MAF_BCS_PATH = "chara/MAF/MAF.bcs";
        private const string FRI_BCS_PATH = "chara/FRI/FRI.bcs";
        private const string NMC_BCS_PATH = "chara/NMC/NMC.bcs";
        
        private enum CusIdType
        {
            ID1,
            ID2
        }

        public enum CusSkillType
        {
            Super,
            Ultimate,
            Evasive,
            Blast,
            Awoken
        }
        public enum SkillFileType
        {
            BAC,
            BDM,
            ShotBDM
        }

        private Install install;
        private List<AliasValue> Aliases = new List<AliasValue>();
        private X2MHelper x2mHelper;

        //Assigned IDs (We keep track of all relevant assigned IDs here, so we dont accidently assign the same ID a second time)
        private List<int> AssignedPartSets = new List<int>();
        private List<int> AssignedCharaIDs = new List<int>();
        private List<string> AssignedCostumes = new List<string>();
        private List<int> AssignedTtbEventIDs = new List<int>();


        public BindingManager(Install install)
        {
            this.install = install;
            x2mHelper = new X2MHelper(install);
        }


        #region Parse
        //Most bindings are now parsed via the ParseStrings method (after the XML is loaded, but before it is loaded into a class proper structure).
        //ParseProperties is only used for AutoIDs (after the XML has been loaded into a class structure). 

        public string ParseString(string str, string path, string attrName)
        {
            return ParseStringBinding<IInstallable>(str, $"Binding for \"{attrName}\".", path, null, null, false);
        }

        /// <summary>
        /// Parse the bindings on all string properties.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="installList">The entries that are being installed (that have the bindings).</param>
        /// <param name="binaryList">The entries that are in the binary list (that we are going to install into).</param>
        /// <param name="filePath">The file path. Used for tracking.</param>
        /// <param name="usedIDs">(Optional) A list of all IDs that are used. This overwrites the default behaviour of checking the Index property on installList and binaryList entries when calculating AutoIDs.</param>
        /// 
        public void ParseProperties<T>(IList<T> installList, IList<T> binaryList, string filePath, List<string> usedIDs = null)
        {
            if (installList == null) return;

            //Safeguard against there not being an existing binary file (very unlikely...)
            if (binaryList == null) binaryList = new List<T>();

            //Parse every single string on classes that implement IInstallable
            foreach (var installEntry in installList)
            {
                ParseProperties_RecursiveSingle(installEntry, filePath, installList, binaryList, usedIDs, true);
            }

            RemoveNullTokenEntries(installList);
        }

        private void ParseProperties_RecursiveList<T>(IEnumerable list, string filePath, IList<T> installList, IList<T> binaryList, List<string> usedIDs = null)
        {
            foreach (var obj in list)
            {
                if (obj != null)
                    ParseProperties_RecursiveSingle<T>(obj, filePath, installList, binaryList, usedIDs);
            }
        }

        private void ParseProperties_RecursiveSingle<T>(object obj, string filePath, IList<T> installList, IList<T> binaryList, List<string> usedIDs = null, bool allowAutoId = false)
        {
            //This property needs to have its props parsed.
            PropertyInfo[] childProps = obj.GetType().GetProperties();

            foreach (var childProp in childProps)
            {
                if (childProp.GetSetMethod() != null && childProp.GetGetMethod() != null)
                {
                    if (childProp.PropertyType == typeof(string))
                    {
                        var autoIdAttr = (BindingAutoId[])childProp.GetCustomAttributes(typeof(BindingAutoId), false);
                        var yaxDontSerializeAttr = (YAXDontSerializeAttribute[])childProp.GetCustomAttributes(typeof(YAXDontSerializeAttribute), false);
                        object value = childProp.GetValue(obj);

                        //Skip if property has YAXDontSerializeAttribute 
                        if (yaxDontSerializeAttr.Length > 0) continue;

                        if (value != null)
                        {
                            if (autoIdAttr.Length > 0)
                            {
                                //Has BindingAutoId attribute.
                                if (allowAutoId)
                                    childProp.SetValue(obj, ParseBinding((string)value, string.Format("{0}", childProp.Name), filePath, installList, binaryList, true, autoIdAttr[0].MaxId, usedIDs).ToString());
                            }
                        }
                    }
                    else if (childProp.PropertyType.IsClass)
                    {
                        object value = childProp.GetValue(obj);
                        var bindingSubClassAtr = (BindingSubClass[])childProp.GetCustomAttributes(typeof(BindingSubClass), false);
                        var bindingSubListAtr = (BindingSubList[])childProp.GetCustomAttributes(typeof(BindingSubList), false);

                        if (bindingSubClassAtr.Length > 0 && value != null)
                        {
                            ParseProperties_RecursiveSingle<T>(value, filePath, installList, binaryList, usedIDs);
                        }
                        if (bindingSubListAtr.Length > 0 && value != null)
                        {
                            if (value is IEnumerable list)
                                ParseProperties_RecursiveList<T>(list, filePath, installList, binaryList, usedIDs);
                        }
                    }
                }
            }
        }

        #endregion

        #region Binding

        private string ParseStringBinding<T>(string str, string comment, string filePath, IEnumerable<T> entries1, IEnumerable<T> entries2, bool allowAutoId = true, ushort maxId = ushort.MaxValue, List<string> usedIds = null)
        {
            //New and improved binding processing method.
            //Now supports multiple bindings within a single string

            if (!DoOnFirstPass(str)) return str;

            while (HasBinding(str))
            {
                int startPos = str.IndexOf('{');
                int endPos = str.IndexOf('}');

                string startStr = str.Substring(0, startPos);
                string endStr = str.Substring(endPos + 1);
                string binding = str.Substring(startPos, endPos - startPos + 1);

                binding = ParseBinding(binding, comment, filePath, entries1, entries2, allowAutoId, maxId, usedIds);

                if (HasBinding(binding))
                    throw new InvalidDataException($"ProcessStringBinding: unexpected result. Binding was not successfuly parsed (binding={binding}).");

                str = $"{startStr}{binding}{endStr}";
            }

            return str;
        }

        private string ParseBinding<T>(string binding, string comment, string filePath, IEnumerable<T> entries1, IEnumerable<T> entries2, bool secondPass = true, ushort maxId = ushort.MaxValue, List<string> usedIds = null)
        {
            if (IsBinding(binding))
            {
                string originalBinding = binding;

                //Return values
                bool retIsString = false;
                string retStr = String.Empty;
                int retID = -1;
                int defaultValue = 0;
          
                List<BindingValue> bindings = ProcessBinding(binding, comment, originalBinding);
                bindings = ValidateBindings(bindings, comment, originalBinding);

                ErrorHandling errorHandler = ErrorHandling.Stop;
                string formating = "0";
                int increment = 0;

                foreach (var b in bindings)
                {
                    switch (b.Function)
                    {
                        case Function.Format:
                            formating = b.GetArgument1();
                            break;
                        case Function.SetAlias:
                            AddAlias(retID + increment, b.GetArgument1());
                            break;
                        case Function.AliasLink:
                            retID = GetAliasId(b.GetArgument1(), comment);
                            break;
                        case Function.SkillID1:
                            {
                                CusSkillType skillType = GetSkillType(b.GetArgument2());
                                retID = GetSkillId(CusIdType.ID1, skillType, b.GetArgument1());
                                break;
                            }
                        case Function.SkillID2:
                            {
                                CusSkillType skillType = GetSkillType(b.GetArgument2());
                                retID = GetSkillId(CusIdType.ID2, skillType, b.GetArgument1());
                                break;
                            }
                        case Function.CharaID:
                            retID = GetCharaId(b.GetArgument1());
                            break;
                        case Function.AutoID:
                            if (!secondPass) throw new Exception(String.Format("The AutoID binding function is not available for this value. ({0})", comment));

                            int minIndex = (!String.IsNullOrWhiteSpace(b.GetArgument1())) ? int.Parse(b.GetArgument1()) : 0;
                            int maxIndex = (!String.IsNullOrWhiteSpace(b.GetArgument2())) ? int.Parse(b.GetArgument2()) : maxId;
                            if (maxIndex > maxId) maxIndex = maxId; //If maxIndex (declared in binding) is greater than maxId (declared on Property), then set maxIndex to maxId (which is the highest possible value)

                            int nextID = GetUnusedIndex(entries1, entries2, minIndex, maxIndex, usedIds);

                            if (nextID == NullTokenInt && errorHandler == ErrorHandling.Stop)
                            {
                                GeneralInfo.SpecialFailState = GeneralInfo.SpecialFailStates.AutoIdBindingFailed;
                                throw new Exception(String.Format("An ID could not be allocated in {2}. Install failed. \n\nBinding: {1}\nProperty: {0}", comment, binding, filePath));
                            }

                            retID = nextID;
                            break;
                        case Function.Error:
                            errorHandler = b.GetErrorHandlingType();
                            break;
                        case Function.DefaultValue:
                            defaultValue = int.Parse(b.GetArgument1());
                            break;
                        case Function.X2MSkillID1:
                            {
                                CusSkillType skillType = GetSkillType(b.GetArgument2());
                                int id1 = x2mHelper.GetX2MSkillID1(b.GetArgument1(), skillType);

                                if (id1 == NullTokenInt && errorHandler == ErrorHandling.Stop)
                                {
                                    GeneralInfo.SpecialFailState = GeneralInfo.SpecialFailStates.X2MNotFound;
                                    throw new Exception(String.Format("Required X2M skill not found. Install failed. \nBinding: {1}\n({0})", comment, binding, filePath));
                                }

                                retID = id1;
                                break;
                            }
                        case Function.X2MSkillID2:
                            {
                                CusSkillType skillType = GetSkillType(b.GetArgument2());
                                int id2 = x2mHelper.GetX2MSkillID2(b.GetArgument1(), skillType);

                                if (id2 == NullTokenInt && errorHandler == ErrorHandling.Stop)
                                {
                                    GeneralInfo.SpecialFailState = GeneralInfo.SpecialFailStates.X2MNotFound;
                                    throw new Exception(String.Format("Required X2M skill not found. Install failed. \nBinding: {1}\n({0})", comment, binding, filePath));
                                }

                                retID = id2;
                                break;
                            }
                        case Function.AutoPartSet:
                            {
                                int min = (b.HasArgument(1)) ? int.Parse(b.GetArgument1()) : 0;
                                int max = (b.HasArgument(2)) ? int.Parse(b.GetArgument2()) : 999;
                                int nextId = GetFreePartSet(min, max);

                                if (nextId == NullTokenInt && errorHandler == ErrorHandling.Stop)
                                {
                                    GeneralInfo.SpecialFailState = GeneralInfo.SpecialFailStates.AutoIdBindingFailed;
                                    throw new Exception(String.Format("A PartSet ID could not be allocated in {2}. Install failed. \n\nBinding: {1}\nProperty: {0}", comment, binding, filePath));
                                }

                                retID = nextId;
                            }
                            break;
                        case Function.Increment:
                            if (!b.HasArgument()) throw new Exception($"No argument found on Increment binding!");

                            if(!int.TryParse(b.GetArgument1(), out increment))
                                throw new Exception($"Error while parsing the argument on Increment binding. (Binding: {binding})");
                            
                            break;
                        case Function.X2MSkillPath:
                            {
                                retIsString = true;
                                CusSkillType skillType = GetSkillType(b.GetArgument2());
                                SkillFileType skillFileType = GetSkillFileType(b.GetArgument3());
                                string x2mSkillPath = x2mHelper.GetX2MSkillPath(b.GetArgument1(), skillType, skillFileType);

                                if (x2mSkillPath == NullTokenStr && errorHandler == ErrorHandling.Stop)
                                {
                                    GeneralInfo.SpecialFailState = GeneralInfo.SpecialFailStates.X2MNotFound;
                                    throw new Exception(String.Format("Required X2M skill not found. Install failed. \nBinding: {1}\n({0})", comment, binding, filePath));
                                }

                                retStr = x2mSkillPath;
                                break;
                            }
                        case Function.AutoCharaID:
                            {
                                int min = (b.HasArgument(1)) ? int.Parse(b.GetArgument1()) : 200;
                                int max = (b.HasArgument(2)) ? int.Parse(b.GetArgument2()) : short.MaxValue;

                                retID = GetFreeCharacterID(min, max);
                                break;
                            }
                        case Function.AutoCostume:
                            {
                                int charaId;
                                string charaCode = "";
                                int min = (b.HasArgument(2)) ? int.Parse(b.GetArgument2()) : 0;
                                int max = (b.HasArgument(3)) ? int.Parse(b.GetArgument3()) : 500;

                                if (!int.TryParse(b.GetArgument1(), out charaId))
                                {
                                    charaCode = b.GetArgument1();
                                    charaId = ((CMS_File)install.GetParsedFile<CMS_File>(CMS_PATH)).CharaCodeToCharaId(charaCode);
                                }
                                else
                                {
                                    charaCode = ((CMS_File)install.GetParsedFile<CMS_File>(CMS_PATH)).CharaIdToCharaCode(charaId);
                                }

                                retID = GetFreeCostume(charaId, charaCode, min, max);

                                break;
                            }
                        case Function.Skip:
                            retID = SkipToken;
                            break;
                        case Function.X2MCharaID:
                            retID = x2mHelper.GetX2MCharaID(b.GetArgument1());

                            if (retID == NullTokenInt && errorHandler == ErrorHandling.Stop)
                            {
                                GeneralInfo.SpecialFailState = GeneralInfo.SpecialFailStates.X2MNotFound;
                                throw new Exception(String.Format("Required X2M character not found. Install failed. \nBinding: {1}\n({0})", comment, binding, filePath));
                            }

                            break;
                        case Function.X2MCharaCode:
                            retIsString = true;
                            retStr = x2mHelper.GetX2MCharaCode(b.GetArgument1());

                            if (retStr == NullTokenStr && errorHandler == ErrorHandling.Stop)
                            {
                                GeneralInfo.SpecialFailState = GeneralInfo.SpecialFailStates.X2MNotFound;
                                throw new Exception(String.Format("Required X2M character not found. Install failed. \nBinding: {1}\n({0})", comment, binding, filePath));
                            }

                            break;
                        case Function.X2MInstalled:
                            retIsString = true;
                            retStr = x2mHelper.IsModInstalled(b.GetArgument1()) ? "true" : "false";
                            break;
                        case Function.LocalKey:
                            retIsString = true;
                            retStr = GeneralInfo.InstallerXmlInfo.GetLocalisedString(b.GetArgument1());
                            break;
                        case Function.IsLanguage:
                            {
                                bool isLang = b.GetArgument1().ToLower() == GeneralInfo.SystemCulture.TwoLetterISOLanguageName.ToLower();
                                retIsString = true;
                                retStr = (isLang) ? "true" : "false";
                            }
                            break;
                        case Function.AutoTtbEvent:
                            retID = GetFreeTtbEventId();
                            break;
                    }
                }

                //If retID == SkipToken or NullToken, then we dont want to increment or format
                if (increment > 0 && (retID == SkipToken || retID == NullTokenInt))
                {
                    increment = 0;
                    formating = "0";
                }

                //Generic error handling code
                if (retID == NullTokenInt && errorHandler == ErrorHandling.Stop)
                {
                    GeneralInfo.SpecialFailState = GeneralInfo.SpecialFailStates.BindingFailed;
                    throw new Exception(String.Format("An ID could not be assigned according to the binding. Install failed. \nBinding: {1}\n({0})", comment, binding));
                }
                else if(retID == NullTokenInt && errorHandler == ErrorHandling.UseDefaultValue)
                {
                    retID = defaultValue;
                }

                //Return the correct type
                if (retIsString)
                {
                    return retStr;
                }
                else
                {
                    return ApplyFormatting(retID + increment, formating);
                }
            }
            else
            {
                //Not a binding.
                return binding;
            }
        }

        public void AddAlias(int ID, string alias)
        {
            Aliases.Add(new AliasValue() { ID = ID, Alias = alias.ToLower() });
        }

        private List<BindingValue> ProcessBinding(string binding, string comment, string originalBinding)
        {
            //Remove spaces
            binding = binding.Trim(' ');

            //Bracket validation
            if (binding[0] != '{') throw new FormatException(String.Format("Cannot find the opening bracket on the binding \"{0}\"\n({1})", originalBinding, comment));
            if (binding[binding.Length - 1] != '}') throw new FormatException(String.Format("Cannot find the closing bracket on the binding \"{0}\"\n({1})", originalBinding, comment));
            if(binding.Count(f => f == '{') > 1) throw new FormatException(String.Format("More than one opening bracket was found on the binding \"{0}\"\n({1})", originalBinding, comment));
            if (binding.Count(f => f == '}') > 1) throw new FormatException(String.Format("More than one closing bracket was found on the binding \"{0}\"\n({1})", originalBinding, comment));

            //Remove brackets
            binding = binding.Trim('{', '}');

            //Parse the bindings
            List<BindingValue> bindings = new List<BindingValue>();
            string[] splitBindings = binding.Split(',');//Regex.Split(binding, @"(?<!,[^(]+\([^)]+),");

            if (splitBindings.Length == 0) throw new FormatException(String.Format("Invalid binding: {0}\n({1})", originalBinding, comment));

            for (int i = 0; i < splitBindings.Length; i++)
            {
                if (String.IsNullOrWhiteSpace(splitBindings[i])) throw new FormatException(String.Format("Param {2} is invalid on binding {0}.\n({1})", originalBinding, comment, i));
                //Split the function up
                var _functionSplit = splitBindings[i].Split('=');
                string function = _functionSplit[0];
                string argument = String.Empty;
                if (_functionSplit.Length == 2) argument = _functionSplit[1];

                //Get arguments
                string[] arguments = argument.Trim().Trim('(', ')').Trim().ToLower().Split(';');
                List<string> arg_temp = new List<string>();

                //Cull any empty args
                foreach(var arg in arguments)
                {
                    if (!string.IsNullOrWhiteSpace(arg))
                        arg_temp.Add(arg);
                }

                arguments = arg_temp.ToArray();

                //Validation
                if (_functionSplit.Length > 2) throw new FormatException(String.Format("Invalid binding argument: {0} (Full binding: {1})\n({1})", splitBindings[i], originalBinding, comment));

                //To lowercase
                function = function.ToLower().Trim(' ');
                argument = argument.ToLower().Trim(' ');

                switch (function)
                {
                    case "autoid":
                        bindings.Add(new BindingValue() {  Function = Function.AutoID, Arguments = arguments });
                        break;
                    case "setalias":
                        bindings.Add(new BindingValue() { Function = Function.SetAlias, Arguments = arguments });
                        break;
                    case "aliaslink":
                        bindings.Add(new BindingValue() { Function = Function.AliasLink, Arguments = arguments });
                        break;
                    case "skillid1":
                        bindings.Add(new BindingValue() { Function = Function.SkillID1, Arguments = arguments });
                        break;
                    case "skillid2":
                        bindings.Add(new BindingValue() { Function = Function.SkillID2, Arguments = arguments });
                        break;
                    case "charaid":
                        bindings.Add(new BindingValue() { Function = Function.CharaID, Arguments = arguments });
                        break;
                    case "error":
                        bindings.Add(new BindingValue() { Function = Function.Error, Arguments = arguments });
                        break;
                    case "defaultvalue":
                        bindings.Add(new BindingValue() { Function = Function.DefaultValue, Arguments = arguments });
                        break;
                    case "x2mskillid1":
                        bindings.Add(new BindingValue() { Function = Function.X2MSkillID1, Arguments = arguments });
                        break;
                    case "x2mskillid2":
                        bindings.Add(new BindingValue() { Function = Function.X2MSkillID2, Arguments = arguments });
                        break;
                    case "autopartset":
                        bindings.Add(new BindingValue() { Function = Function.AutoPartSet, Arguments = arguments });
                        break;
                    case "format":
                        bindings.Add(new BindingValue() { Function = Function.Format, Arguments = arguments });
                        break;
                    case "increment":
                        bindings.Add(new BindingValue() { Function = Function.Increment, Arguments = arguments });
                        break;
                    case "x2mskillpath":
                        bindings.Add(new BindingValue() { Function = Function.X2MSkillPath, Arguments = arguments });
                        break;
                    case "autocostume":
                        bindings.Add(new BindingValue() { Function = Function.AutoCostume, Arguments = arguments });
                        break;
                    case "skip":
                        bindings.Add(new BindingValue() { Function = Function.Skip, Arguments = arguments });
                        break;
                    case "autocharaid":
                        bindings.Add(new BindingValue() { Function = Function.AutoCharaID, Arguments = arguments });
                        break;
                    case "x2mcharaid":
                        bindings.Add(new BindingValue() { Function = Function.X2MCharaID, Arguments = arguments });
                        break;
                    case "x2mcharacode":
                        bindings.Add(new BindingValue() { Function = Function.X2MCharaCode, Arguments = arguments });
                        break;
                    case "x2minstalled":
                        bindings.Add(new BindingValue() { Function = Function.X2MInstalled, Arguments = arguments });
                        break;
                    case "islang":
                        bindings.Add(new BindingValue() { Function = Function.IsLanguage, Arguments = arguments });
                        break;
                    case "localkey":
                        bindings.Add(new BindingValue() { Function = Function.LocalKey, Arguments = arguments });
                        break;
                    case "autottbevent":
                        bindings.Add(new BindingValue() { Function = Function.AutoTtbEvent, Arguments = arguments });
                        break;
                    default:
                        throw new FormatException(String.Format("Invalid ID Binding Function (Function = {0}, Argument = {1})\nFull binding: {2}", function, argument, originalBinding));
                }


            }

            return bindings;
        }

        private List<BindingValue> ValidateBindings(List<BindingValue> bindings, string comment, string originalBinding)
        {
            //Ensures the bindings are valid, and orders them correctly so the alias function comes last (if present)

            //Entries must be ordered like this: Error > ID > Increment > Alias
            MoveFunctionToStart(bindings, Function.Error);
            MoveFunctionToLast(bindings, Function.Increment);
            MoveFunctionToLast(bindings, Function.SetAlias);

            //Validate functions
            bool hasIdBinding = false;
            bool hasAliasBinding = false;
            bool hasErrorBinding = false;
            bool hasDefaultValueBinding = false;
            bool hasFormatBinding = false;
            bool hasIncrementBinding = false;

            for (int i = 0; i < bindings.Count; i++)
            {
                switch (bindings[i].Function)
                {
                    case Function.SetAlias:
                        if (hasAliasBinding) throw new Exception(String.Format("More than one instance of {0} found. Binding parse failed.\n({1})", Function.SetAlias, comment));
                        hasAliasBinding = true;
                        break;
                    case Function.Error:
                        if (hasErrorBinding) throw new Exception(String.Format("More than one instance of {0} found. Binding parse failed.\n({1})", Function.Error, comment));
                        hasErrorBinding = true;
                        break;
                    case Function.DefaultValue:
                        if (hasDefaultValueBinding) throw new Exception(String.Format("More than one instance of {0} found. Binding parse failed.\n({1})", Function.DefaultValue, comment));
                        hasDefaultValueBinding = true;
                        break;
                    case Function.Format:
                        if (hasFormatBinding) throw new Exception(String.Format("More than one instance of {0} found. Binding parse failed.\n({1})", Function.Format, comment));
                        hasFormatBinding = true;
                        break;
                    case Function.Increment:
                        if (hasIncrementBinding) throw new Exception(String.Format("More than one instance of {0} found. Binding parse failed.\n({1})", Function.Increment, comment));
                        hasIncrementBinding = true;
                        break;
                    default:
                        if (hasIdBinding) throw new Exception(String.Format("More than one instance of an ID binding found within the same binding. Binding parse failed.\n({0})", comment));
                        hasIdBinding = true;
                        break;
                }
            }

            if (hasIdBinding == false && hasAliasBinding == true) throw new Exception("The SetAlias binding cannot be used without also a ID binding.");
            if (hasIdBinding == false && hasErrorBinding == true) throw new Exception("The Error binding cannot be used without also a ID binding.");

            //Validate arguments
            for (int i = 0; i < bindings.Count; i++)
            {
                switch (bindings[i].Function)
                {
                    case Function.AutoTtbEvent:
                    case Function.Skip:
                        //Cant have arguments
                        if (bindings[i].HasArgument()) throw new Exception(String.Format("The {0} binding function takes no arguments, but {2} was found\n({1})", bindings[i].Function, comment, bindings[i].Arguments.Length));
                        break;
                    case Function.AutoID:
                    case Function.AutoCharaID:
                    case Function.AutoPartSet:
                        //Can have no arguments or have arguments
                        break;
                    case Function.AliasLink:
                    case Function.SetAlias:
                    case Function.CharaID:
                    case Function.Error:
                    case Function.DefaultValue:
                    case Function.AutoCostume:
                    case Function.X2MCharaID:
                    case Function.X2MCharaCode:
                    case Function.X2MInstalled:
                    case Function.LocalKey:
                    case Function.IsLanguage:
                        //Must have an argument
                        if (!bindings[i].HasArgument()) throw new Exception(String.Format("The {0} binding function takes a string argument, but none was found.\n({1})", bindings[i].Function, comment));
                        break;
                    case Function.SkillID1:
                    case Function.SkillID2:
                    case Function.X2MSkillID1:
                    case Function.X2MSkillID2:
                    case Function.X2MSkillPath:
                        if (bindings[i].Arguments.Length < 2)
                        {
                            throw new Exception(String.Format("The {0} binding function takes 2 string arguments minimum, but only {1} were found. \n({2})", bindings[i].Function, bindings[i].Arguments.Length, comment));
                        }
                        break;
                }

            }

            return bindings;
        }


        //Helpers
        private bool IsBinding(string binding)
        {
            if (string.IsNullOrWhiteSpace(binding)) return false;
            binding = binding.Trim();
            if (binding[0] == '{' || binding[binding.Length - 1] == '}') return true;
            return false;
        }

        public bool HasBinding(string binding)
        {
            if (string.IsNullOrWhiteSpace(binding)) return false;
            return (binding.Contains('{') && binding.Contains('}'));
        }

        private bool DoOnFirstPass(string binding)
        {
            if (string.IsNullOrWhiteSpace(binding)) return false;
            return (!binding.ToLower().Contains("autoid"));
        }

        private void MoveFunctionToLast(List<BindingValue> bindings, Function func)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].Function == func)
                {
                    var function = bindings[i];
                    bindings.RemoveAt(i);
                    bindings.Add(function);
                    break;
                }
            }
        }

        private void MoveFunctionToStart(List<BindingValue> bindings, Function func)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].Function == func)
                {
                    var error = bindings[i];
                    bindings.RemoveAt(i);
                    bindings.Insert(0, error);
                    break;
                }
            }
        }


        #endregion

        #region Function Helpers
        private int GetAliasId(string alias, string comment)
        {
            foreach(var a in Aliases)
            {
                if (a.Alias == alias) return a.ID;
            }

            throw new Exception(String.Format("Could not find the alias: {0}. Binding parse failed. ({1})\n\nThis is most likely caused by using a binding that relies on something to be installed before it. To fix this issue, you can simply add a DoLast=\"True\" tag onto the File entry which will cause the file to be installed last.", alias, comment));
        }

        /// <summary>
        /// Tries to find a unused ID betwen min and max within the two specified lists. Returns the NullToken if an ID cannot be allocated.
        /// </summary>
        /// <returns></returns>
        public int GetUnusedIndex<T>(IEnumerable<T> entries1, IEnumerable<T> entries2, int minIndex = 0, int maxIndex = -1, List<string> usedIds = null)
        {
            //todo: review this code
            if (maxIndex == -1) maxIndex = UInt16.MaxValue - 1;
            List<int> UsedIndexes = new List<int>();

            //Create UsedIndexes
            if(usedIds != null)
            {
                foreach (var usedId in usedIds)
                {
                    int value = 0;
                    if (int.TryParse(usedId, out value))
                    {
                        UsedIndexes.Add(value);
                    }
                }
            }
            else
            {
                if (entries1 != null)
                {
                    foreach (var e in entries1)
                    {
                        try
                        {
                            if(e is IInstallable installable)
                                UsedIndexes.Add(installable.SortID);
                        }
                        catch
                        {

                        }
                    }
                }

                if (entries2 != null)
                {
                    foreach (var e in entries2)
                    {
                        try
                        {
                            if (e is IInstallable installable)
                                UsedIndexes.Add(installable.SortID);
                        }
                        catch
                        {

                        }
                    }
                }

            }
            

            //Create UnusedIndexes
            int idx = 0;
            while (true)
            {
                //If maxIndex has been reached
                if (idx > maxIndex && maxIndex != -1)
                {
                    return NullTokenInt;
                }

                //If the index is not used the idx is greater than minIndex
                if (!UsedIndexes.Contains(idx) && idx >= minIndex)
                {
                    if(usedIds != null)
                    {
                        usedIds.Add(idx.ToString());
                    }
                    return idx;
                }
                idx++;
                if (idx > ushort.MaxValue) return -1; //Safe-guard code (very unlikely case)
            }
        }
        
        private CusSkillType GetSkillType(string argument)
        {
            switch (argument.ToLower())
            {
                case "super":
                    return CusSkillType.Super;
                case "ultimate":
                    return CusSkillType.Ultimate;
                case "evasive":
                    return CusSkillType.Evasive;
                case "blast":
                    return CusSkillType.Blast;
                case "awoken":
                    return CusSkillType.Awoken;
                default:
                    return CusSkillType.Super;
            }
        }

        private SkillFileType GetSkillFileType(string argument)
        {
            switch (argument.ToLower())
            {
                case "bac":
                    return SkillFileType.BAC;
                case "bdm":
                    return SkillFileType.BDM;
                case "shotbdm":
                    return SkillFileType.ShotBDM;
                default:
                    return SkillFileType.BAC;
            }
        }

        private int GetSkillId(CusIdType idType, CusSkillType skillType, string shortName)
        {
            CUS_File cusFile = (CUS_File)install.GetParsedFile<CUS_File>(CUS_PATH);

            List<Skill> skills = null;
            switch (skillType)
            {
                case CusSkillType.Super:
                    skills = cusFile.SuperSkills;
                    break;
                case CusSkillType.Ultimate:
                    skills = cusFile.UltimateSkills;
                    break;
                case CusSkillType.Evasive:
                    skills = cusFile.EvasiveSkills;
                    break;
                case CusSkillType.Blast:
                    skills = cusFile.BlastSkills;
                    break;
                case CusSkillType.Awoken:
                    skills = cusFile.AwokenSkills;
                    break;
            }

            foreach(var skill in skills)
            {
                if(skill.ShortName.ToLower() == shortName)
                {
                    switch (idType)
                    {
                        case CusIdType.ID1:
                            return skill.ID1;
                        case CusIdType.ID2:
                            return skill.ID2;
                    }
                }
            }

            return NullTokenInt; //Skill wasn't found
        }

        private int GetCharaId(string shortName)
        {
            CMS_File cmsFile = (CMS_File)install.GetParsedFile<CMS_File>(CMS_PATH);

            foreach (var chara in cmsFile.CMS_Entries)
            {
                if (chara.Str_04.ToLower() == shortName) return int.Parse(chara.Index);
            }

            return NullTokenInt; //Chara wasn't found, so defaulting to 0
        }

        private int GetFreePartSet(int min, int max)
        {
            int id = min;

            while (IsPartSetUsed(id) && id < max)
            {
                id++;
            }

            if (IsPartSetUsed(id))
                return NullTokenInt;

            AssignedPartSets.Add(id);
            return id;
        }

        private bool IsPartSetUsed(int partSet)
        {
            if (AssignedPartSets.Contains(partSet)) return true;
            if (((BCS_File)install.GetParsedFile<BCS_File>(HUM_BCS_PATH)).PartSets.Any(x => x.ID == partSet)) return true;
            if (((BCS_File)install.GetParsedFile<BCS_File>(HUF_BCS_PATH)).PartSets.Any(x => x.ID == partSet)) return true;
            if (((BCS_File)install.GetParsedFile<BCS_File>(MAM_BCS_PATH)).PartSets.Any(x => x.ID == partSet)) return true;
            if (((BCS_File)install.GetParsedFile<BCS_File>(MAF_BCS_PATH)).PartSets.Any(x => x.ID == partSet)) return true;
            if (((BCS_File)install.GetParsedFile<BCS_File>(FRI_BCS_PATH)).PartSets.Any(x => x.ID == partSet)) return true;
            if (((BCS_File)install.GetParsedFile<BCS_File>(NMC_BCS_PATH)).PartSets.Any(x => x.ID == partSet)) return true;
            return false;
        }
        
        private string ApplyFormatting(int value, string formatting)
        {
            switch (formatting)
            {
                case "0":
                    return value.ToString();
                case "1":
                    return value.ToString("D1");
                case "2":
                    return value.ToString("D2");
                case "3":
                    return value.ToString("D3");
                case "4":
                    return value.ToString("D4");
                case "5":
                    return value.ToString("D5");
                case "6":
                    return value.ToString("D6");
                case "7":
                    return value.ToString("D7");
                case "8":
                    return value.ToString("D8");
                case "9":
                    return value.ToString("D9");
                case "10":
                    return value.ToString("D10");
                default:
                    throw new Exception("ApplyFormatting: Invalid formating. Please use a number between 1 and 10.");
            }
        }

        public void ProcessSkipBindings(object entry, object entryToReplace)
        {
            //Final pass for processing the "Skip" binding. This is done late as it needs to be done just before the entry is installed, where we can know if it is replacing an existing entry.
            if (entryToReplace == null || entry == null) return;

            PropertyInfo[] childProps = entry.GetType().GetProperties();

            foreach (var childProp in childProps)
            {
                if (childProp.GetSetMethod() != null && childProp.GetGetMethod() != null)
                {
                    if (childProp.PropertyType == typeof(string) || (childProp.PropertyType.IsPrimitive && childProp.PropertyType.IsValueType))
                    {
                        object value = childProp.GetValue(entry);
                        bool skipValue = false;

                        if(childProp.PropertyType == typeof(string))
                        {
                            int valueInt;
                            if (int.TryParse((string)value, out valueInt))
                            {
                                if((string)value == SkipTokenStr)
                                    skipValue = true;
                            }
                        }
                        else
                        {
                            try
                            {
                                if ((short)value == SkipToken)
                                    skipValue = true;
                            }
                            catch (InvalidCastException) {}
                        }

                        if (skipValue)
                        {
                            childProp.SetValue(entry, childProp.GetValue(entryToReplace));
                        }

                    }
                    else if (childProp.PropertyType.IsClass)
                    {
                        object classObject = childProp.GetValue(entry);
                        object classObjectToReplace = childProp.GetValue(entryToReplace);


                        if (classObject is IList list && classObjectToReplace is IList listToReplace)
                        {
                            if(list.Count == listToReplace.Count)
                            {
                                for (int i = 0; i < list.Count; i++)
                                {
                                    ProcessSkipBindings(list[i], listToReplace[i]);
                                }
                            }
                        }
                        else if (classObject is Array array && classObjectToReplace is Array arrayToReplace)
                        {
                            if (array.Length == arrayToReplace.Length)
                            {
                                for (int i = 0; i < array.Length; i++)
                                {
                                    ProcessSkipBindings(array.GetValue(i), arrayToReplace.GetValue(i));
                                }
                            }
                        }
                        else
                        {
                            ProcessSkipBindings(classObject, classObjectToReplace);
                        }
                    }
                }
            }
        }

        public void ProcessInstallerXmlBindings(InstallerXml installerXml)
        {
            if(installerXml.UiOverrides != null)
            {
                ProcessUiOverrides(installerXml.UiOverrides.InstallConfirm);
                ProcessUiOverrides(installerXml.UiOverrides.UninstallConfirm);
                ProcessUiOverrides(installerXml.UiOverrides.ReinstallInitialPrompt);
                ProcessUiOverrides(installerXml.UiOverrides.UpdateInitialPrompt);
                ProcessUiOverrides(installerXml.UiOverrides.DowngradeInitialPrompt);
            }

            foreach(var step in installerXml.InstallOptionSteps)
            {
                step.IsEnabled = ParseString(step.IsEnabled, GeneralInfo.InstallerXml, "Step.IsEnabled");
                step.Name = ParseString(step.Name, GeneralInfo.InstallerXml, "Step.Message");
                step.Message = ParseString(step.Message, GeneralInfo.InstallerXml, "Step.Message");

                if (step.OptionList != null)
                {
                    foreach (var option in step.OptionList)
                    {
                        option.Name = ParseString(option.Name, GeneralInfo.InstallerXml, "Option.Name");
                        option.Tooltip = ParseString(option.Tooltip, GeneralInfo.InstallerXml, "Option.Tooltip");

                        if (option.Paths != null)
                        {
                            foreach(var file in option.Paths)
                            {
                                file.IsEnabled = ParseString(file.IsEnabled, GeneralInfo.InstallerXml, "File.IsEnabled");
                                //file.InstallPath = ParseString(file.InstallPath, GeneralInfo.InstallerXml, "InstallPath"); //We do this later, as the file is installed. This way we can access any aliases that have been set.
                            }
                        }
                    }
                }
            }

            foreach(var file in installerXml.InstallFiles)
            {
                file.IsEnabled = ParseString(file.IsEnabled, GeneralInfo.InstallerXml, "File.IsEnabled");
                //file.InstallPath = ParseString(file.InstallPath, GeneralInfo.InstallerXml, "InstallPath"); //We do this later, as the file is installed. This way we can access any aliases that have been set.
            }
        }

        private void ProcessUiOverrides(InstallStep step)
        {
            if(step != null)
            {
                step.Name = ParseString(step.Name, GeneralInfo.InstallerXml, "Step.Message");
                step.Message = ParseString(step.Message, GeneralInfo.InstallerXml, "Step.Message");
            }
        }

        //Characters:
        private int GetFreeCharacterID(int min, int max)
        {
            int current = min;

            while (IsCharacterIdUsed(current))
            {
                current++;

                if (current >= max) return -1;
            }

            AssignedCharaIDs.Add(current);
            return current;
        }

        private bool IsCharacterIdUsed(int charaId)
        {
            if (AssignedCharaIDs.Contains(charaId)) return true;
            if (((CMS_File)install.GetParsedFile<CMS_File>(CMS_PATH)).CMS_Entries.Any(x => x.ID == charaId)) return true;
            if (((CSO_File)install.GetParsedFile<CSO_File>(CSO_PATH)).CsoEntries.Any(x => x.CharaID == charaId)) return true;
            if (((AUR_File)install.GetParsedFile<AUR_File>(AUR_PATH)).CharacterAuras.Any(x => x.CharaID == charaId)) return true;
            if (((PSC_File)install.GetParsedFile<PSC_File>(PSC_PATH)).CharacterExists(charaId)) return true;

            return false;
        }

        //Costumes:
        private int GetFreeCostume(int charaId, string charaCode, int min, int max)
        {
            int current = min;

            while (IsCostumeUsed(charaId, charaCode, current))
            {
                current++;

                if (current >= max) return NullTokenInt;
            }

            AssignedCostumes.Add((string.IsNullOrWhiteSpace(charaCode)) ? $"{charaId}_{current}" : $"{charaCode}_{current}");

            return current;
        }

        private bool IsCostumeUsed(int charaId, string charaCode, int costume)
        {
            //Check if costume is used in the patcher slot file. 
            //We need to check if this exists before we try to use it, since it may not exist and the installer doesn't create it in that case.
            if (install.GetParsedFile<CharaSlotsFile>(CharaSlotsFile.FILE_NAME_BIN, false, false) != null)
            {
                if (((CharaSlotsFile)install.GetParsedFile<CharaSlotsFile>(CharaSlotsFile.FILE_NAME_BIN, false, false)).SlotExists(charaCode, costume)) return true;
            }

            if (((CSO_File)install.GetParsedFile<CSO_File>(CSO_PATH)).CsoEntries.Any(x => x.CharaID == charaId && x.Costume == costume)) return true;
            if (((AUR_File)install.GetParsedFile<AUR_File>(AUR_PATH)).CharacterAuras.Any(x => x.CharaID == charaId && x.Costume == costume)) return true;
            if (((PSC_File)install.GetParsedFile<PSC_File>(PSC_PATH)).CostumeExists(charaId, costume)) return true;
            if (((HCI_File)install.GetParsedFile<HCI_File>(HCI_PATH)).Entries.Any(x => x.CharaID == charaId && x.Costume == costume)) return true;


            if (AssignedCostumes.Contains($"{charaCode}_{costume}")) return true;
            if (AssignedCostumes.Contains($"{charaId}_{costume}")) return true;

            return false;
        }

        //TtbEvent
        private int GetFreeTtbEventId()
        {
            int current = 1200;

            while (IsTtbEventIdUsed(current))
            {
                current++;

                if (current >= int.MaxValue) return NullTokenInt;
            }
            
            AssignedTtbEventIDs.Add(current);

            return current;
        }

        private bool IsTtbEventIdUsed(int id)
        {
            if (((TTB_File)install.GetParsedFile<TTB_File>(TTB_PATH)).IsEventIdUsed(id)) return true;
            if (AssignedTtbEventIDs.Contains(id)) return true;

            return false;
        }

        #endregion

        #region Misc

        /// <summary>
        /// Removes all entries that has a NullToken (caused by a failed AutoID or X2M binding).
        /// </summary>
        private void RemoveNullTokenEntries<T>(IList<T> installList)
        {
            List<T> toDelete = new List<T>();

            foreach (var e in installList)
            {
                if (HasNullToken(e)) toDelete.Add(e);
            }

            foreach (var e in toDelete)
            {
                installList.Remove(e);
            }

            //installList.RemoveAll(e => HasNullToken(e));
        }

        private bool HasNullToken<T>(T entry)
        {
            PropertyInfo[] properties = entry.GetType().GetProperties();

            foreach (var prop in properties)
            {
                if (prop.PropertyType == typeof(string))
                {
                    if ((string)prop.GetValue(entry) == NullTokenStr)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

    }


    public struct AliasValue
    {
        public string Alias { get; set; }
        public int ID { get; set; }
    }

    public struct BindingValue
    {
        public Function Function { get; set; }
        public string[] Arguments { get; set; }

        public string GetArgument1()
        {
            if (Arguments != null)
            {
                if (Arguments.Length > 0)
                {
                    return Arguments[0].ToLower().Trim();
                }
            }

            return String.Empty;
        }

        public string GetArgument2()
        {
            if (Arguments != null)
            {
                if (Arguments.Length > 1)
                {
                    return Arguments[1].ToLower().Trim();
                }
            }

            return String.Empty;
        }

        public string GetArgument3()
        {
            if (Arguments != null)
            {
                if (Arguments.Length > 2)
                {
                    return Arguments[2].ToLower().Trim();
                }
            }

            return String.Empty;
        }

        public string GetArgument4()
        {
            if (Arguments != null)
            {
                if (Arguments.Length > 3)
                {
                    return Arguments[3].ToLower().Trim();
                }
            }

            return String.Empty;
        }

        public bool HasArgument(int arg = 1)
        {
            if (Arguments != null)
            {
                if (Arguments.Length >= arg)
                {
                    return true;
                }
            }
            return false;
        }

        public ErrorHandling GetErrorHandlingType()
        {
            if (Function == Function.Error)
            {
                string args = GetArgument1().ToLower();
                switch (args)
                {
                    case "skip":
                        return ErrorHandling.Skip;
                    case "stop":
                        return ErrorHandling.Stop;
                    case "usedefaultvalue":
                    case "default":
                    case "defaultvalue":
                        return ErrorHandling.UseDefaultValue;
                    default:
                        throw new Exception(String.Format("{0} is not a valid Error argument.", args));
                }

            }
            else
            {
                throw new Exception(String.Format("Function {0} cannot access the ErrorHandling type.", Function));
            }
        }
    }

    public enum Function
    {
        AutoID,
        AutoPartSet,
        AutoCharaID,
        AutoCostume,
        AutoTtbEvent,
        AliasLink,
        SkillID1,
        SkillID2,
        CharaID,
        X2MCharaID,
        X2MCharaCode,
        X2MSkillID1,
        X2MSkillID2,
        Skip,

        //For use in InstallerXml:
        X2MSkillPath,
        X2MInstalled,
        LocalKey,
        IsLanguage,

        //Auxiliary functions:
        Format,
        Error,
        DefaultValue,
        SetAlias,
        Increment
    }

    public enum ErrorHandling
    {
        Skip,
        Stop,
        UseDefaultValue
    }


}
