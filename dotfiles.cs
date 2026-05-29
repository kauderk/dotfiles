// @ai_gen(gemini 3 flash extended)
using System;
using System.IO;
using System.Collections.Generic;

class BackupItem {
    public string dir;
    public string alias;
    public BackupItem[] sub_items;
}

struct ResolvedPath {
    public string system_path;
    public string local_destination;
    public bool is_dir_type;
}

class Program {
    // this program handles advanced backup synchronization logic with structural recursive definitions and preventive conflict guards
    static void Main(string[] args) {
        string script_dir = AppDomain.CurrentDomain.BaseDirectory;
        string app_data = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string local_app_data = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        BackupItem[] backup_items = new BackupItem[] {
            new BackupItem { dir = Path.Combine(local_app_data, @"Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState\settings.json") },

            new BackupItem { dir = Path.Combine(app_data, @"alacritty\alacritty.toml") },

            new BackupItem {
                alias = "vscode",
                sub_items = new BackupItem[] {
                    new BackupItem { dir = Path.Combine(app_data, @"Code\User\settings.json") },
                    new BackupItem { dir = Path.Combine(app_data, @"Code\User\keybindings.json") },
                    new BackupItem { dir = Path.Combine(app_data, @"Code\User\snippets") },
                    new BackupItem { dir = Path.Combine(app_data, @"Code\User\globalStorage\state.vscdb") },
                    new BackupItem { dir = Path.Combine(app_data, @"Code\extensions") }
                }
            }
        };

        List<ResolvedPath> flat_list = new List<ResolvedPath>();
        Action<BackupItem[], string> resolve_items = null;

        resolve_items = (items, current_prefix) => {
            foreach (BackupItem item in items) {
                string next_prefix = current_prefix;
                if (!string.IsNullOrEmpty(item.alias)) {
                    next_prefix = Path.Combine(current_prefix, item.alias);
                }

                if (item.sub_items != null) {
                    resolve_items(item.sub_items, next_prefix);
                } else if (!string.IsNullOrEmpty(item.dir)) {
                    string leaf_name = Path.GetFileName(item.dir);
                    string local_dest = Path.Combine(next_prefix, leaf_name);
                    
                    bool is_dir = Directory.Exists(item.dir);
                    if (!is_dir && !File.Exists(item.dir)) {
                        is_dir = string.IsNullOrEmpty(Path.GetExtension(item.dir));
                    }

                    ResolvedPath resolved;
                    resolved.system_path = item.dir;
                    resolved.local_destination = local_dest;
                    resolved.is_dir_type = is_dir;
                    
                    flat_list.Add(resolved);
                }
            }
        };

        resolve_items(backup_items, "");

        if (args.Length == 0 || args[0] == "+help") {
            Console.WriteLine("commands:");
            Console.WriteLine("  +pull  back up local repo copies to last_pull, then copy active system configs here");
            Console.WriteLine("  +push  back up active system configs to last_push, then overwrite them with repo copies");
            Console.WriteLine("  +help  show this menu");
            return;
        }

        string command = args[0];

        if (command == "+pull") {
            bool has_conflict = false;

            for (int i = 0; i < flat_list.Count; i++) {
                for (int j = i + 1; j < flat_list.Count; j++) {
                    ResolvedPath item_a = flat_list[i];
                    ResolvedPath item_b = flat_list[j];

                    string norm_a = item_a.local_destination.ToLower();
                    string norm_b = item_b.local_destination.ToLower();

                    if (norm_a == norm_b) {
                        if (item_a.system_path.ToLower() != item_b.system_path.ToLower()) {
                            Console.WriteLine("error: conflict detected during execution guards");
                            Console.WriteLine("type: exact path collision");
                            Console.WriteLine("sub-type: multiple structural targets resolve to the same local repository destination file");
                            Console.WriteLine("local destination: " + item_a.local_destination);
                            Console.WriteLine("source path alpha: " + item_a.system_path);
                            Console.WriteLine("source path beta:  " + item_b.system_path);
                            Console.WriteLine();
                            has_conflict = true;
                        }
                    }

                    if (norm_a.StartsWith(norm_b + @"\") && !item_b.is_dir_type) {
                        Console.WriteLine("error: conflict detected during execution guards");
                        Console.WriteLine("type: structural namespace collision");
                        Console.WriteLine("sub-type: a tracking file target matches a container directory name defined by another nested alias item");
                        Console.WriteLine("conflicting route: " + item_b.local_destination);
                        Console.WriteLine("file source:   " + item_b.system_path);
                        Console.WriteLine("nested source: " + item_a.system_path);
                        Console.WriteLine();
                        has_conflict = true;
                    }

                    if (norm_b.StartsWith(norm_a + @"\") && !item_a.is_dir_type) {
                        Console.WriteLine("error: conflict detected during execution guards");
                        Console.WriteLine("type: structural namespace collision");
                        Console.WriteLine("sub-type: a tracking file target matches a container directory name defined by another nested alias item");
                        Console.WriteLine("conflicting route: " + item_a.local_destination);
                        Console.WriteLine("file source:   " + item_a.system_path);
                        Console.WriteLine("nested source: " + item_b.system_path);
                        Console.WriteLine();
                        has_conflict = true;
                    }
                }
            }

            if (has_conflict) {
                Console.WriteLine("operation aborted due to unresolved leaf conflicts");
                Environment.Exit(1);
            }

            string last_pull_dir = Path.Combine(script_dir, "last_pull");
            if (Directory.Exists(last_pull_dir)) {
                Directory.Delete(last_pull_dir, true);
            }
            Directory.CreateDirectory(last_pull_dir);

            foreach (ResolvedPath resolved_item in flat_list) {
                string absolute_local = Path.Combine(script_dir, resolved_item.local_destination);
                string backup_dest = Path.Combine(last_pull_dir, resolved_item.local_destination);

                if (File.Exists(absolute_local)) {
                    string target_dir = Path.GetDirectoryName(backup_dest);
                    if (!Directory.Exists(target_dir)) {
                        Directory.CreateDirectory(target_dir);
                    }
                    File.Copy(absolute_local, backup_dest, true);
                } else if (Directory.Exists(absolute_local)) {
                    CopyDirectory(absolute_local, backup_dest);
                }
            }

            foreach (ResolvedPath resolved_item in flat_list) {
                string absolute_local = Path.Combine(script_dir, resolved_item.local_destination);

                if (resolved_item.is_dir_type) {
                    if (Directory.Exists(resolved_item.system_path)) {
                        if (Directory.Exists(absolute_local)) {
                            Directory.Delete(absolute_local, true);
                        }
                        CopyDirectory(resolved_item.system_path, absolute_local);
                        Console.WriteLine("collected folder: " + resolved_item.local_destination);
                    } else {
                        Console.WriteLine("skipped, system folder missing: " + resolved_item.system_path);
                    }
                } else {
                    if (File.Exists(resolved_item.system_path)) {
                        string target_dir = Path.GetDirectoryName(absolute_local);
                        if (!Directory.Exists(target_dir)) {
                            Directory.CreateDirectory(target_dir);
                        }
                        File.Copy(resolved_item.system_path, absolute_local, true);
                        Console.WriteLine("collected file: " + resolved_item.local_destination);
                    } else {
                        Console.WriteLine("skipped, system file missing: " + resolved_item.system_path);
                    }
                }
            }
            Console.WriteLine("pull operation finished safely");
        } else if (command == "+push") {
            string last_push_dir = Path.Combine(script_dir, "last_push");
            if (Directory.Exists(last_push_dir)) {
                Directory.Delete(last_push_dir, true);
            }
            Directory.CreateDirectory(last_push_dir);

            foreach (ResolvedPath resolved_item in flat_list) {
                string backup_dest = Path.Combine(last_push_dir, resolved_item.local_destination);

                if (File.Exists(resolved_item.system_path)) {
                    string target_dir = Path.GetDirectoryName(backup_dest);
                    if (!Directory.Exists(target_dir)) {
                        Directory.CreateDirectory(target_dir);
                    }
                    File.Copy(resolved_item.system_path, backup_dest, true);
                } else if (Directory.Exists(resolved_item.system_path)) {
                    CopyDirectory(resolved_item.system_path, backup_dest);
                }
            }

            foreach (ResolvedPath resolved_item in flat_list) {
                string absolute_local = Path.Combine(script_dir, resolved_item.local_destination);

                if (resolved_item.is_dir_type) {
                    if (Directory.Exists(absolute_local)) {
                        if (Directory.Exists(resolved_item.system_path)) {
                            Directory.Delete(resolved_item.system_path, true);
                        }
                        CopyDirectory(absolute_local, resolved_item.system_path);
                        Console.WriteLine("overwrote system folder: " + resolved_item.system_path);
                    } else {
                        Console.WriteLine("skipped, local folder missing: " + resolved_item.local_destination);
                    }
                } else {
                    if (File.Exists(absolute_local)) {
                        string target_dir = Path.GetDirectoryName(resolved_item.system_path);
                        if (!Directory.Exists(target_dir)) {
                            Directory.CreateDirectory(target_dir);
                        }
                        File.Copy(absolute_local, resolved_item.system_path, true);
                        Console.WriteLine("overwrote system file: " + resolved_item.system_path);
                    } else {
                        Console.WriteLine("skipped, local file missing: " + resolved_item.local_destination);
                    }
                }
            }
            Console.WriteLine("push operation finished safely");
        } else {
            Console.WriteLine("unknown flag: " + command);
        }
    }

    static void CopyDirectory(string source_dir, string dest_dir) {
        // this function recursively copies all files and subdirectories from a source folder to a destination folder
        Directory.CreateDirectory(dest_dir);
        foreach (string file in Directory.GetFiles(source_dir)) {
            string dest_file = Path.Combine(dest_dir, Path.GetFileName(file));
            File.Copy(file, dest_file, true);
        }
        foreach (string sub_dir in Directory.GetDirectories(source_dir)) {
            string dest_sub_dir = Path.Combine(dest_dir, Path.GetFileName(sub_dir));
            CopyDirectory(sub_dir, dest_sub_dir);
        }
    }
}