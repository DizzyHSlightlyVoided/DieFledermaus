﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace DieFledermaus.Cli.Globalization {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class TextResources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal TextResources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("DieFledermaus.Cli.Globalization.TextResources", typeof(TextResources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Press any key to continue..
        /// </summary>
        internal static string AnyKey {
            get {
                return ResourceManager.GetString("AnyKey", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Cancel.
        /// </summary>
        internal static string Cancel {
            get {
                return ResourceManager.GetString("Cancel", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Comment:.
        /// </summary>
        internal static string Comment {
            get {
                return ResourceManager.GetString("Comment", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to All operations completed successfully..
        /// </summary>
        internal static string Completed {
            get {
                return ResourceManager.GetString("Completed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No entry file or files are set..
        /// </summary>
        internal static string CreateNoEntry {
            get {
                return ResourceManager.GetString("CreateNoEntry", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Directory not found: {0}.
        /// </summary>
        internal static string DirNotFound {
            get {
                return ResourceManager.GetString("DirNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to This project is under development. Features and functionality are subject to change without warning..
        /// </summary>
        internal static string Disclaimer {
            get {
                return ResourceManager.GetString("Disclaimer", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The key or password is incorrect!.
        /// </summary>
        internal static string EncryptedBadKey {
            get {
                return ResourceManager.GetString("EncryptedBadKey", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The archive is encrypted..
        /// </summary>
        internal static string EncryptedEx {
            get {
                return ResourceManager.GetString("EncryptedEx", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Encrypted entry: {0}.
        /// </summary>
        internal static string EncryptedExEntry {
            get {
                return ResourceManager.GetString("EncryptedExEntry", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Enter password.
        /// </summary>
        internal static string EncryptedPrompt1Pwd {
            get {
                return ResourceManager.GetString("EncryptedPrompt1Pwd", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Enter encryption key (hexadecimal).
        /// </summary>
        internal static string EncryptedPrompt2KeyHex {
            get {
                return ResourceManager.GetString("EncryptedPrompt2KeyHex", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Enter encryption key (base64).
        /// </summary>
        internal static string EncryptedPrompt3KeyB64 {
            get {
                return ResourceManager.GetString("EncryptedPrompt3KeyB64", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The specified key is not a valid Base64 value..
        /// </summary>
        internal static string EncryptInvalidBase64 {
            get {
                return ResourceManager.GetString("EncryptInvalidBase64", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The specified key is not a valid hexadecimal value..
        /// </summary>
        internal static string EncryptInvalidHex {
            get {
                return ResourceManager.GetString("EncryptInvalidHex", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The number of bytes in the specified key is invalid..
        /// </summary>
        internal static string EncryptInvalidKeyLength {
            get {
                return ResourceManager.GetString("EncryptInvalidKeyLength", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Encryption can only be used in interactive mode..
        /// </summary>
        internal static string EncryptionNoOpts {
            get {
                return ResourceManager.GetString("EncryptionNoOpts", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to An archive can only be decrypted in interactive mode..
        /// </summary>
        internal static string EncryptionNoOptsEx {
            get {
                return ResourceManager.GetString("EncryptionNoOptsEx", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Key size must be equal to {0} bits ({1} bytes)..
        /// </summary>
        internal static string EncryptKeyLength {
            get {
                return ResourceManager.GetString("EncryptKeyLength", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Parameter &quot;{0}&quot; only makes sense when encrypting..
        /// </summary>
        internal static string ErrorHide {
            get {
                return ResourceManager.GetString("ErrorHide", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No archive file set..
        /// </summary>
        internal static string ExtractNoArchive {
            get {
                return ResourceManager.GetString("ExtractNoArchive", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The specified file does not exist: {0}.
        /// </summary>
        internal static string FileNotFound {
            get {
                return ResourceManager.GetString("FileNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to archive.
        /// </summary>
        internal static string HelpArchive {
            get {
                return ResourceManager.GetString("HelpArchive", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Compress a file:.
        /// </summary>
        internal static string HelpCompress {
            get {
                return ResourceManager.GetString("HelpCompress", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Decompress a file:.
        /// </summary>
        internal static string HelpDecompress {
            get {
                return ResourceManager.GetString("HelpDecompress", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Show extended help:.
        /// </summary>
        internal static string HelpHelp {
            get {
                return ResourceManager.GetString("HelpHelp", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to input.
        /// </summary>
        internal static string HelpInput {
            get {
                return ResourceManager.GetString("HelpInput", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to List files verbosely:.
        /// </summary>
        internal static string HelpList {
            get {
                return ResourceManager.GetString("HelpList", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Encrypt using AES encryption..
        /// </summary>
        internal static string HelpMAes {
            get {
                return ResourceManager.GetString("HelpMAes", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The archive file to extract from or create..
        /// </summary>
        internal static string HelpMArchive {
            get {
                return ResourceManager.GetString("HelpMArchive", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Set to create a new archive..
        /// </summary>
        internal static string HelpMCreate {
            get {
                return ResourceManager.GetString("HelpMCreate", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The files to compress..
        /// </summary>
        internal static string HelpMEntry {
            get {
                return ResourceManager.GetString("HelpMEntry", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to When extracting, lists the files you want to extract. Wildcards accepted..
        /// </summary>
        internal static string HelpMEntry2 {
            get {
                return ResourceManager.GetString("HelpMEntry2", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Set to extract from an existing archive..
        /// </summary>
        internal static string HelpMExtract {
            get {
                return ResourceManager.GetString("HelpMExtract", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Displays this extended help..
        /// </summary>
        internal static string HelpMHelp {
            get {
                return ResourceManager.GetString("HelpMHelp", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Encrypts the entire directory structure..
        /// </summary>
        internal static string HelpMHide {
            get {
                return ResourceManager.GetString("HelpMHide", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Run in interactive mode, ask for user input when needed..
        /// </summary>
        internal static string HelpMInteractive {
            get {
                return ResourceManager.GetString("HelpMInteractive", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Lists all files in an archive..
        /// </summary>
        internal static string HelpMList {
            get {
                return ResourceManager.GetString("HelpMList", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Specifies output directory when extracting..
        /// </summary>
        internal static string HelpMOut {
            get {
                return ResourceManager.GetString("HelpMOut", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Overwite all existing files..
        /// </summary>
        internal static string HelpMOverwrite {
            get {
                return ResourceManager.GetString("HelpMOverwrite", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Compress to a DieFledermaus stream instead of a DieFledermauZ archive..
        /// </summary>
        internal static string HelpMSingle {
            get {
                return ResourceManager.GetString("HelpMSingle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Skip existing files..
        /// </summary>
        internal static string HelpMSkip {
            get {
                return ResourceManager.GetString("HelpMSkip", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Display more information..
        /// </summary>
        internal static string HelpMVerbose {
            get {
                return ResourceManager.GetString("HelpMVerbose", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to output.
        /// </summary>
        internal static string HelpOutput {
            get {
                return ResourceManager.GetString("HelpOutput", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Keep it secret. Keep it safe..
        /// </summary>
        internal static string KeepSecret {
            get {
                return ResourceManager.GetString("KeepSecret", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to (Encrypted file #{0}).
        /// </summary>
        internal static string ListEncryptedEntry {
            get {
                return ResourceManager.GetString("ListEncryptedEntry", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to (Encrypted item #{0}).
        /// </summary>
        internal static string ListEncryptedUnknown {
            get {
                return ResourceManager.GetString("ListEncryptedUnknown", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The following parameters are mutually exclusive: {0}.
        /// </summary>
        internal static string MutuallyExclusive {
            get {
                return ResourceManager.GetString("MutuallyExclusive", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No entries to compress!.
        /// </summary>
        internal static string NoEntriesCreate {
            get {
                return ResourceManager.GetString("NoEntriesCreate", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Parameter &quot;{0}&quot; doesn&apos;t make sense in extract-mode..
        /// </summary>
        internal static string NoEntryExtract {
            get {
                return ResourceManager.GetString("NoEntryExtract", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Parameter &quot;{0}&quot; doesn&apos;t make sense in create-mode..
        /// </summary>
        internal static string NoOutputCreate {
            get {
                return ResourceManager.GetString("NoOutputCreate", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Output-directory only makes sense when extracting..
        /// </summary>
        internal static string OutDirOnly {
            get {
                return ResourceManager.GetString("OutDirOnly", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to no.
        /// </summary>
        internal static string OverNo {
            get {
                return ResourceManager.GetString("OverNo", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to rename.
        /// </summary>
        internal static string OverRename {
            get {
                return ResourceManager.GetString("OverRename", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Overwriting..
        /// </summary>
        internal static string Overwrite {
            get {
                return ResourceManager.GetString("Overwrite", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to File already exists: {0}.
        /// </summary>
        internal static string OverwriteAlert {
            get {
                return ResourceManager.GetString("OverwriteAlert", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Overwrite? (Y)es/(N)o/(R)ename.
        /// </summary>
        internal static string OverwritePrompt {
            get {
                return ResourceManager.GetString("OverwritePrompt", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Renaming to {0}.
        /// </summary>
        internal static string OverwriteRename {
            get {
                return ResourceManager.GetString("OverwriteRename", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Archive file is the same as the input file! {0}.
        /// </summary>
        internal static string OverwriteSameEntry {
            get {
                return ResourceManager.GetString("OverwriteSameEntry", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Skipping..
        /// </summary>
        internal static string OverwriteSkip {
            get {
                return ResourceManager.GetString("OverwriteSkip", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to yes.
        /// </summary>
        internal static string OverYes {
            get {
                return ResourceManager.GetString("OverYes", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Multiple contradictory values for parameter &quot;{0}&quot;: {1}.
        /// </summary>
        internal static string ParamDup {
            get {
                return ResourceManager.GetString("ParamDup", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Multiple contradictory values for compressed file: {0}.
        /// </summary>
        internal static string ParamDupLit {
            get {
                return ResourceManager.GetString("ParamDupLit", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Parameters:.
        /// </summary>
        internal static string Parameters {
            get {
                return ResourceManager.GetString("Parameters", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid parameter format: {0}.
        /// </summary>
        internal static string ParamInvalid {
            get {
                return ResourceManager.GetString("ParamInvalid", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Parameter &quot;{0}&quot; does not take an argument..
        /// </summary>
        internal static string ParamNoArg {
            get {
                return ResourceManager.GetString("ParamNoArg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Parameter &quot;{0}&quot; requries an argument..
        /// </summary>
        internal static string ParamReqArg {
            get {
                return ResourceManager.GetString("ParamReqArg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unknown parameter &quot;{0}&quot;..
        /// </summary>
        internal static string ParamUnknown {
            get {
                return ResourceManager.GetString("ParamUnknown", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The password must have at least one character..
        /// </summary>
        internal static string PasswordZeroLength {
            get {
                return ResourceManager.GetString("PasswordZeroLength", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to archive.
        /// </summary>
        internal static string PNameArchive {
            get {
                return ResourceManager.GetString("PNameArchive", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to create.
        /// </summary>
        internal static string PNameCreate {
            get {
                return ResourceManager.GetString("PNameCreate", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to extract.
        /// </summary>
        internal static string PNameExtract {
            get {
                return ResourceManager.GetString("PNameExtract", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to file.
        /// </summary>
        internal static string PNameFile {
            get {
                return ResourceManager.GetString("PNameFile", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to help.
        /// </summary>
        internal static string PNameHelp {
            get {
                return ResourceManager.GetString("PNameHelp", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to hide.
        /// </summary>
        internal static string PNameHide {
            get {
                return ResourceManager.GetString("PNameHide", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to interactive.
        /// </summary>
        internal static string PNameInteractive {
            get {
                return ResourceManager.GetString("PNameInteractive", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to list.
        /// </summary>
        internal static string PNameList {
            get {
                return ResourceManager.GetString("PNameList", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to out.
        /// </summary>
        internal static string PNameOut {
            get {
                return ResourceManager.GetString("PNameOut", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to output.
        /// </summary>
        internal static string PNameOutput {
            get {
                return ResourceManager.GetString("PNameOutput", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to overWrite.
        /// </summary>
        internal static string PNameOverwrite {
            get {
                return ResourceManager.GetString("PNameOverwrite", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to single.
        /// </summary>
        internal static string PNameSingle {
            get {
                return ResourceManager.GetString("PNameSingle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to skip.
        /// </summary>
        internal static string PNameSkip {
            get {
                return ResourceManager.GetString("PNameSkip", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to skip-existing.
        /// </summary>
        internal static string PNameSkipExisting {
            get {
                return ResourceManager.GetString("PNameSkipExisting", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to verbose.
        /// </summary>
        internal static string PNameVerbose {
            get {
                return ResourceManager.GetString("PNameVerbose", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Filename not set! Changing output path to: {0}.
        /// </summary>
        internal static string RenameExtract {
            get {
                return ResourceManager.GetString("RenameExtract", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to At least one of the following arguments are required: {0}.
        /// </summary>
        internal static string RequireAtLeastOne {
            get {
                return ResourceManager.GetString("RequireAtLeastOne", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Successfully extracted {0} files ({1} failed).
        /// </summary>
        internal static string SuccessExtract {
            get {
                return ResourceManager.GetString("SuccessExtract", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Created: {0}.
        /// </summary>
        internal static string TimeC {
            get {
                return ResourceManager.GetString("TimeC", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to read file&apos;s creation time: {0}.
        /// </summary>
        internal static string TimeCGet {
            get {
                return ResourceManager.GetString("TimeCGet", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to set file&apos;s creation time: {0}.
        /// </summary>
        internal static string TimeCSet {
            get {
                return ResourceManager.GetString("TimeCSet", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Modified: {0}.
        /// </summary>
        internal static string TimeM {
            get {
                return ResourceManager.GetString("TimeM", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to read file&apos;s last modified time: {0}.
        /// </summary>
        internal static string TimeMGet {
            get {
                return ResourceManager.GetString("TimeMGet", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to set file&apos;s last modified time: {0}.
        /// </summary>
        internal static string TimeMSet {
            get {
                return ResourceManager.GetString("TimeMSet", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to DieFlede command-line utility.
        /// </summary>
        internal static string Title {
            get {
                return ResourceManager.GetString("Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to (Unnamed file).
        /// </summary>
        internal static string UnnamedFile {
            get {
                return ResourceManager.GetString("UnnamedFile", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Usage:.
        /// </summary>
        internal static string Usage {
            get {
                return ResourceManager.GetString("Usage", resourceCulture);
            }
        }
    }
}
