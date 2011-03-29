﻿#region  Copyright (c) 2011 LibGit2Sharp committers

//  The MIT License
//  
//  Copyright (c) 2011 LibGit2Sharp committers
//  
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
//  
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
//  
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//  THE SOFTWARE.

#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using LibGit2Sharp.Properties;

namespace LibGit2Sharp
{
    /// <summary>
    ///   A Repository is the primary interface into a git repository
    /// </summary>
    public class Repository : IDisposable
    {
        private const char posixDirectorySeparatorChar = '/';
        private readonly BranchCollection branches;
        private readonly CommitCollection commits;
        private readonly RepositoryOptions options;
        private readonly ReferenceCollection refs;
        private readonly IntPtr repo = IntPtr.Zero;

        private bool disposed;

        /// <summary>
        ///   Initializes a new instance of the <see cref = "Repository" /> class.
        /// 
        ///   Exceptions:
        ///   ArgumentException
        ///   ArgumentNullException
        ///   TODO: ApplicationException is thrown for all git errors right now
        /// </summary>
        /// <param name = "path">The path to the git repository to open.</param>
        /// <param name = "options">The options.</param>
        public Repository(string path, RepositoryOptions options = null)
        {
            Path = path;
            Ensure.ArgumentNotNull(path, "path");
            this.options = options ?? new RepositoryOptions();

            Path = path;
            PosixPath = path.Replace(System.IO.Path.DirectorySeparatorChar, posixDirectorySeparatorChar);

            if (!this.options.CreateIfNeeded && !Directory.Exists(path))
                throw new ArgumentException(Resources.RepositoryDoesNotExist, "path");

            if (this.options.CreateIfNeeded)
            {
                var res = NativeMethods.git_repository_init(out repo, PosixPath, this.options.IsBareRepository);
                Ensure.Success(res);
            }
            else
            {
                var res = NativeMethods.git_repository_open(out repo, PosixPath);
                Ensure.Success(res);
            }
            commits = new CommitCollection(this);
            refs = new ReferenceCollection(this);
            branches = new BranchCollection(this);
        }

        internal IntPtr RepoPtr
        {
            get { return repo; }
        }

        public ReferenceCollection Refs
        {
            get { return refs; }
        }

        public CommitCollection Commits
        {
            get { return commits.StartingAt(Refs.Head()); }
        }

        public BranchCollection Branches
        {
            get { return branches; }
        }

        /// <summary>
        ///   Gets the path to the git repository.
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        ///   Gets the posix path to the git repository.
        /// </summary>
        public string PosixPath { get; private set; }

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        private void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!disposed)
            {
                // If disposing equals true, dispose all managed
                // and unmanaged resources.
                if (disposing)
                {
                    // Dispose managed resources.
                }

                // Call the appropriate methods to clean up
                // unmanaged resources here.
                NativeMethods.git_repository_free(repo);

                // Note disposing has been done.
                disposed = true;
            }
        }

        /// <summary>
        ///   Tells if the specified <see cref = "GitOid" /> exists in the repository.
        /// 
        ///   Exceptions:
        ///   ArgumentNullException
        /// </summary>
        /// <param name = "oid">The oid.</param>
        /// <returns></returns>
        public bool Exists(GitOid oid)
        {
            Ensure.ArgumentNotNull(oid, "oid");

            var odb = NativeMethods.git_repository_database(repo);
            return NativeMethods.git_odb_exists(odb, ref oid);
        }

        /// <summary>
        ///   Tells if the specified sha exists in the repository.
        /// 
        ///   Exceptions:
        ///   ArgumentException
        ///   ArgumentNullException
        /// </summary>
        /// <param name = "sha">The sha.</param>
        /// <returns></returns>
        public bool Exists(string sha)
        {
            Ensure.ArgumentNotNullOrEmptyString(sha, "sha");

            var oid = GitOid.FromSha(sha);
            return Exists(oid);
        }

        ~Repository()
        {
            // Do not re-create Dispose clean-up code here.
            // Calling Dispose(false) is optimal in terms of
            // readability and maintainability.
            Dispose(false);
        }

        private GitObject Lookup(GitOid oid, GitObjectType type = GitObjectType.Any, bool throwIfNotFound = true)
        {
            Ensure.ArgumentNotNull(oid, "oid");

            IntPtr obj;
            var res = NativeMethods.git_object_lookup(out obj, repo, ref oid, type);
            if (res == (int) GitErrorCode.GIT_ENOTFOUND)
            {
                if (throwIfNotFound)
                {
                    throw new KeyNotFoundException(string.Format(CultureInfo.CurrentCulture, Resources.ObjectNotFoundInRepository, oid));
                }
                return null;
            }
            Ensure.Success(res);

            return GitObject.CreateFromPtr(obj, oid, this);
        }

        /// <summary>
        ///   Lookup an object by it's <see cref = "GitOid" />. An exception will be thrown if the object is not found.
        /// 
        ///   Exceptions:
        ///   ArgumentNullException
        /// </summary>
        /// <param name = "oid">The oid.</param>
        /// <param name = "type">The <see cref = "GitObjectType" /> of the object to lookup.</param>
        /// <returns></returns>
        public GitObject Lookup(GitOid oid, GitObjectType type = GitObjectType.Any)
        {
            return Lookup(oid, type, true);
        }

        /// <summary>
        ///   Lookup an object by it's sha. An exception will be thrown if the object is not found.
        /// 
        ///   Exceptions:
        ///   ArgumentException
        ///   ArgumentNullException
        /// </summary>
        /// <param name = "sha">The sha to lookup.</param>
        /// <param name = "type"></param>
        /// <returns>the <see cref = "GitObject" />.</returns>
        public GitObject Lookup(string sha, GitObjectType type = GitObjectType.Any)
        {
            Ensure.ArgumentNotNullOrEmptyString(sha, "sha");

            var oid = GitOid.FromSha(sha);
            return Lookup(oid, type);
        }

        /// <summary>
        ///   Lookup an object by it's sha. An exception will be thrown if the object is not found.
        /// </summary>
        /// <typeparam name = "T"></typeparam>
        /// <param name = "sha">The sha to lookup.</param>
        /// <returns>the <see cref = "GitObject" />.</returns>
        public T Lookup<T>(string sha) where T : GitObject
        {
            return (T) Lookup(sha, GitObject.TypeToTypeMap[typeof (T)]);
        }

        /// <summary>
        ///   Lookup an object by it's <see cref = "GitOid" />. An exception will be thrown if the object is not found.
        /// </summary>
        /// <typeparam name = "T"></typeparam>
        /// <param name = "oid">The oid.</param>
        /// <returns></returns>
        public T Lookup<T>(GitOid oid) where T : GitObject
        {
            return (T) Lookup(oid, GitObject.TypeToTypeMap[typeof (T)]);
        }

        /// <summary>
        ///   Trys to lookup an object by it's sha.
        /// </summary>
        /// <typeparam name = "T"></typeparam>
        /// <param name = "sha">The sha to lookup.</param>
        /// <returns></returns>
        public T TryLookup<T>(string sha) where T : GitObject
        {
            return (T) TryLookup(sha, GitObject.TypeToTypeMap[typeof (T)]);
        }

        /// <summary>
        ///   Try to lookup an object by it's sha. If an object is not found null will be returned.
        /// 
        ///   Exceptions:
        ///   ArgumentNullException
        /// </summary>
        /// <param name = "sha">The sha to lookup.</param>
        /// <param name = "type"></param>
        /// <returns>the <see cref = "GitObject" /> or null if it was not found.</returns>
        public GitObject TryLookup(string sha, GitObjectType type = GitObjectType.Any)
        {
            Ensure.ArgumentNotNullOrEmptyString(sha, "sha");

            var oid = GitOid.FromSha(sha);
            return TryLookup(oid, type);
        }

        /// <summary>
        ///   Try to lookup an object by it's <see cref = "GitOid" />. If an object is not found null will be returned.
        /// </summary>
        /// <param name = "oid">The oid to lookup.</param>
        /// <param name = "type"></param>
        /// <returns>the <see cref = "GitObject" /> or null if it was not found.</returns>
        public GitObject TryLookup(GitOid oid, GitObjectType type = GitObjectType.Any)
        {
            return Lookup(oid, type, false);
        }
    }
}