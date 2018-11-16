using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Widget;
using System.Collections.Generic;
using Android.Content;
using Android.Provider;

using System;
using System.Diagnostics;
using Android.Support.V4.Provider;

namespace Xamarin_Android_Directory_Traversing
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        #region PRIVATE MEMBER VARS
        private const int OPEN_DOCUMENT_TREE_ACTIVITY = 1;

        private Android.Net.Uri _userSelectedDirectory = null;
        private Android.Net.Uri _parentUri = null;

        private List<string> _filenames = new List<string>();
        private ListView _filesListView = null;
        #endregion

        #region LIFECYCLE METHODS
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            // start the file explorer on start, have user select
            // the directory to perform this test in
            Intent intent = new Intent(Intent.ActionOpenDocumentTree);
            StartActivityForResult(intent, OPEN_DOCUMENT_TREE_ACTIVITY);

            _filesListView = FindViewById<ListView>(Resource.Id.filesLV);
            Button docFileBtn = FindViewById<Button>(Resource.Id.docFileBtn);
            Button docContractBtn = FindViewById<Button>(Resource.Id.docContractBtn);

            // set click handlers
            docContractBtn.Click += TraverseDirectory_ClickHandler;
            docFileBtn.Click += TraverseDirectory_ClickHandler;
        }

        /// <summary>
        /// Handles the result from activities called with StartActivityForResult()
        /// </summary>
        /// <param name="requestCode"></param>
        /// <param name="resultCode"></param>
        /// <param name="data"></param>
        protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent data)
        {
            try
            {
                base.OnActivityResult(requestCode, resultCode, data);

                if (resultCode == Result.Ok && data != null && requestCode == OPEN_DOCUMENT_TREE_ACTIVITY)
                {
                    // keep track of the raw URI data returned from user selection
                    // just in case we need it later?
                    _userSelectedDirectory = data.Data;

                    // keep track of the parent directory
                    // this is used in building document URIs of the children
                    // based on their id
                    this._parentUri = DocumentsContract.BuildDocumentUriUsingTree(
                        _userSelectedDirectory,
                        DocumentsContract.GetTreeDocumentId(_userSelectedDirectory)
                        );

                    // WARN: This is a little hacky, and might not be that robust
                    // After creating the initial file strucutre at the location,
                    // I would recommend commenting the below check/function call
                    var dir = DocumentFile.FromTreeUri(this, _parentUri);
                    if (dir.IsDirectory && dir.Length() == 0)
                        CreateTestFileStructureAtLocation();

                    // persist permissions for this directory
                    ContentResolver.TakePersistableUriPermission(
                        _userSelectedDirectory,
                        ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission
                        );

                    data.AddFlags(ActivityFlags.GrantWriteUriPermission);
                    data.AddFlags(ActivityFlags.GrantReadUriPermission);
                    data.AddFlags(ActivityFlags.GrantPrefixUriPermission);
                    data.AddFlags(ActivityFlags.GrantPersistableUriPermission);
                }
            }
            catch (Exception ex)
            {
                var timeElapsedTV = FindViewById<TextView>(Resource.Id.timeElapsedTV);
                SetTextViewOnError(timeElapsedTV, ex);
            }
        }
        #endregion

        #region DocumentsContract Method
        /// <summary>
        /// Use DocumentsContract API to read the file structure at the given location iteratively
        /// includes children
        /// 
        /// from https://stackoverflow.com/questions/41096332/issues-traversing-through-directory-hierarchy-with-android-storage-access-framew\
        /// </summary>
        private void DocumentsContractGetFilesFromSelectedFolder()
        {
            // build the children structure of the root directory
            var childrenUri = DocumentsContract.BuildChildDocumentsUriUsingTree(
                _userSelectedDirectory,
                DocumentsContract.GetTreeDocumentId(_userSelectedDirectory)
                );

            // this stack is used in processing subsequent subdirectories
            // when we encounter one in traversing, push it on the stack to be processed
            // we continue until this stack is empty
            //
            // NOTE: stacks are LIFO, therefore this will produce a depth-first approach
            Stack<Android.Net.Uri> dirNodes = new Stack<Android.Net.Uri>();
            dirNodes.Push(childrenUri);

            // used to keep track of how many directories we've traversed so far
            // this only works because we know the structure of the directory!!!
            int i = 0;
            while (dirNodes.Count != 0)
            {
                // get the next subdirectory
                dirNodes.TryPop(out childrenUri);

                // using this sub directory URI, query it for document information
                // the current seach finds all documents in the tree and returns the below columns: ID, Name, and Mime-type
                // searches can be customized using remaining three arguments (currently null which is why we return everything)
                var cursor = this.ContentResolver.Query(
                    childrenUri,
                    new string[] { DocumentsContract.Document.ColumnDocumentId, DocumentsContract.Document.ColumnDisplayName, DocumentsContract.Document.ColumnMimeType },
                    null, null, null
                    );

                // for each of the documents returned from our search,
                while (cursor.MoveToNext())
                {
                    var docId = cursor.GetString(0);
                    var name = cursor.GetString(1);
                    var mime = cursor.GetString(2);

                    // only add the text files we've added
                    // this is just for demonstration purposes
                    if (mime == "text/plain")
                    {
                        // TODO: figure out a way to get directory (aka parent) name here
                        // Add the file name to the list of files found so far
                        _filenames.Add($"Director {i}/{name}");
                    }
                    // if this is a directory, push its URI to the directory nodes stack for later processing
                    else if (mime == DocumentsContract.Document.MimeTypeDir)
                    {
                        dirNodes.Push(
                            DocumentsContract.BuildChildDocumentsUriUsingTree(
                                _parentUri,
                                docId));
                    }
                }
                ++i;
                // cleanup cursor
                cursor.Dispose();
            }
            return;
        }
        #endregion

        #region DocumentFile.ListFiles() Method
        /// <summary>
        /// Calls ScanFilesInSelectedFolder to begin scanning the initial directory
        /// </summary>
        /// <param name="folder">location chosen by user</param>
        /// <returns>Hashset of all the files in given location</returns>
        internal void GetFilesFromSelectedFolder()
        {
            ScanFilesInSelectedFolder(
                DocumentFile.FromTreeUri(this, _userSelectedDirectory)
                );
        }

        private void ScanFilesInSelectedFolder(DocumentFile folder)
        {
            var Items = folder.ListFiles();

            foreach (var ItemType in Items)
            {
                if (ItemType.IsDirectory)
                {
                    ScanFilesInSelectedFolder(ItemType);
                }
                else
                {
                    var parentDirName = ItemType.ParentFile.Name;
                    _filenames.Add($"{parentDirName}/{ItemType.Name}");
                }
            }
        }
        #endregion

        #region HELPERS
        private void TraverseDirectory_ClickHandler(object sender, EventArgs args)
        {
            int sendId = (sender as Button).Id;

            try
            {

                var watch = Stopwatch.StartNew();
                switch (sendId)
                {
                    case Resource.Id.docFileBtn:
                        GetFilesFromSelectedFolder();
                        break;
                    case Resource.Id.docContractBtn:
                        DocumentsContractGetFilesFromSelectedFolder();
                        break;
                    case Resource.Id.fileApiBtn:
                        throw new NotImplementedException("Haven't gotten that far yet!");
                    default:
                        throw new InvalidOperationException($"Cannot handler sender with ID: {sendId}");
                }
                watch.Stop();

                TextView timeElapsedTextView = FindViewById<TextView>(Resource.Id.timeElapsedTV);
                timeElapsedTextView.Text = $"Time Elapsed: {watch.ElapsedMilliseconds}ms";

                // there's got to be a better way to do this!
                _filesListView.Adapter?.Dispose();
                _filesListView.Adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleListItem1, _filenames);
            }
            catch (Exception ex)
            {
                var timeElapsedTV = FindViewById<TextView>(Resource.Id.timeElapsedTV);
                SetTextViewOnError(timeElapsedTV, ex);
            }
        }

        private bool CreateTestFileStructureAtLocation()
        {
            try
            {
                Android.Net.Uri dirUri = DocumentsContract.BuildDocumentUriUsingTree(
                    _userSelectedDirectory,
                    DocumentsContract.GetTreeDocumentId(_userSelectedDirectory)
                    );

                for (int i = 0; i < 10; ++i)
                {
                    // create directory
                    var newDir = DocumentsContract.CreateDocument(
                            this.ContentResolver,
                            dirUri,
                            DocumentsContract.Document.MimeTypeDir,
                            $"Directory {i + 1}"
                            );
                    if (newDir == null)
                        throw new Exception("Unable to create directory at location!");

                    for (int j = 0; j < 100; ++j)
                    {
                        var newFile = DocumentsContract.CreateDocument(
                            this.ContentResolver,
                            newDir,
                            "text/plain",
                            $"file{j + 1}.txt");
                        if (newFile == null)
                            throw new Exception("Unable to create file at location!");
                    }
                }
            }
            catch (Exception ex)
            {
                // set our text view to display the error
                var timeElapsedTV = FindViewById<TextView>(Resource.Id.timeElapsedTV);
                SetTextViewOnError(timeElapsedTV, ex);
            }

            return true;
        }

        private void SetTextViewOnError(TextView tv, Exception ex)
        {
            if (tv != null)
            {
                tv.Text = $"ERR: {ex.Message} \n {ex.StackTrace}";
                tv.SetTextColor(Android.Graphics.Color.Red);
            }
        }
        #endregion
    }
}