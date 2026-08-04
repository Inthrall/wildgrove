// <copyright file="PluginVersion.cs" company="Google Inc.">
// Copyright (C) 2014 Google Inc. All Rights Reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//    limitations under the License.
// </copyright>

namespace GooglePlayGames
{
    public class PluginVersion
    {
        // Current Version.
        // Patched locally. Upstream 2.2.0 shipped this file still reading 2.1.0
        // (package.json says 2.2.0), so GPGSUpgrader stamped "2.1.0" into
        // ProjectSettings/GooglePlayGameSettings.txt on a successful upgrade —
        // which reads exactly like an upgrade that never ran. Re-apply on the
        // next re-vendor if Google still hasn't bumped it.
        public const int VersionInt = 0x20200;
        public const string VersionString = "2.2.0";
        public const string VersionKey = "20200";
    }
}
