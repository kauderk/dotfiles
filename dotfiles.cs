// @ai_gen(gemini 3 flash extended)
// this program processes structured directory filters using scoped brace blocks and flat prefix arrays to safely synchronize files with conflict validation guards
using System;
using System.IO;
using System.Collections.Generic;

class BackupItem {
    public string dot_dir;
    public string alias;
    public string dir;
    public string dir_filter;
}

struct FilterBlock {
    public string block_prefix;
    public string block_directive;
    public List<string> rule_paths;
}

struct ResolvedPath {
    public string system_path;
    public string local_destination;
    public bool is_dir_type;
    public List<FilterBlock> filters;
}

class Program {
    static void Main(string[] args) {
        string script_dir = AppDomain.CurrentDomain.BaseDirectory;
        string app_data = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string user_profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        BackupItem[] backup_items = new BackupItem[] {
            new BackupItem {
                dot_dir = "APPDATA",
                alias = "vscode",
                dir = Path.Combine(app_data, "Code"),
                dir_filter = @"
                    > includes_filter
                    User\settings.json
                    User/keybindings.json

                    # include everthing inside snippets folder
                    User/snippets/

                    User/tasks.json

                    # include everything inside profiles, except for 'User/profiles/builtin'
                    User/profiles/ > excludes_filter {
                        User/profiles/builtin/
                    }

                    User/globalStorage/state.vscdb
                    User/globalStorage/storage.json
                "
            },
            new BackupItem {
                dot_dir = "USER",
                dir = Path.Combine(user_profile, ".vscode"),
                dir_filter = @"
                    # explicit use of '> includes_filter'
                    extensions/extensions.json
                    argv.json
                "
            }
        };

        List<ResolvedPath> flat_list = new List<ResolvedPath>();

        foreach (BackupItem item in backup_items) {
            if (string.IsNullOrEmpty(item.dir)) {
                continue;
            }

            bool is_dir = Directory.Exists(item.dir);
            if (!is_dir) {
                if (!File.Exists(item.dir)) {
                    is_dir = string.IsNullOrEmpty(Path.GetExtension(item.dir));
                }
            }

            if (!is_dir) {
                if (!string.IsNullOrEmpty(item.dir_filter)) {
                    Console.WriteLine("error: dir_filter can only be used when dir is a folder");
                    Environment.Exit(1);
                }
            }

            string local_dest = "";
            if (!string.IsNullOrEmpty(item.alias)) {
                local_dest = item.alias;
            } else {
                local_dest = Path.GetFileName(item.dir);
            }

            List<FilterBlock> parsed_blocks = new List<FilterBlock>();
            
            FilterBlock root_block;
            root_block.block_prefix = "";
            root_block.block_directive = "includes_filter";
            root_block.rule_paths = new List<string>();

            FilterBlock active_block = root_block;
            bool inside_custom_block = false;

            if (!string.IsNullOrEmpty(item.dir_filter)) {
                char[] newline_chars = new char[] { '\r', '\n' };
                string[] lines = item.dir_filter.Split(newline_chars, StringSplitOptions.RemoveEmptyEntries);

                foreach (string raw_line in lines) {
                    string clean_line = raw_line.Trim();
                    
                    if (clean_line.Contains("#")) {
                        int hash_index = clean_line.IndexOf('#');
                        clean_line = clean_line.Substring(0, hash_index).Trim();
                    }

                    if (string.IsNullOrEmpty(clean_line)) {
                        continue;
                    }

                    if (clean_line == "}") {
                        parsed_blocks.Add(active_block);
                        active_block = root_block;
                        inside_custom_block = false;
                        continue;
                    }

                    if (clean_line.StartsWith(">")) {
                        string directive_type = clean_line.Substring(1).Trim();
                        root_block.block_directive = directive_type;
                        active_block.block_directive = directive_type;
                        continue;
                    }

                    if (clean_line.Contains("{")) {
                        int brace_index = clean_line.IndexOf('{');
                        string block_decl = clean_line.Substring(0, brace_index).Trim();
                        
                        string block_prefix = "";
                        string block_directive = "includes_filter";

                        if (block_decl.Contains(">")) {
                            int arrow_index = block_decl.IndexOf('>');
                            block_prefix = block_decl.Substring(0, arrow_index).Trim();
                            block_directive = block_decl.Substring(arrow_index + 1).Trim();
                        } else {
                            block_prefix = block_decl;
                        }

                        block_prefix = block_prefix.Replace('\\', '/').Trim('/');
                        
                        active_block.block_prefix = block_prefix;
                        active_block.block_directive = block_directive;
                        active_block.rule_paths = new List<string>();
                        inside_custom_block = true;
                        continue;
                    }

                    string normalized_rule = clean_line.Replace('\\', '/');
                    bool ends_with_slash = normalized_rule.EndsWith("/");
                    normalized_rule = normalized_rule.Trim('/');
                    
                    if (ends_with_slash) {
                        normalized_rule = normalized_rule + "/";
                    }

                    if (inside_custom_block) {
                        active_block.rule_paths.Add(normalized_rule);
                    } else {
                        root_block.rule_paths.Add(normalized_rule);
                    }
                }
            }

            if (inside_custom_block) {
                parsed_blocks.Add(active_block);
            }
            parsed_blocks.Insert(0, root_block);

            ResolvedPath resolved;
            resolved.system_path = item.dir;
            resolved.local_destination = local_dest;
            resolved.is_dir_type = is_dir;
            resolved.filters = parsed_blocks;
            
            flat_list.Add(resolved);
        }

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

            // display planned synchronization map before execution
            Console.WriteLine("execution preview for +pull:");
            foreach (ResolvedPath resolved_item in flat_list) {
                string absolute_local = Path.Combine(script_dir, resolved_item.local_destination);
                string preview_pull_dir = Path.Combine(script_dir, "last_pull");
                string backup_dest = Path.Combine(preview_pull_dir, resolved_item.local_destination);

                Console.WriteLine("  target name: " + resolved_item.local_destination);
                if (resolved_item.is_dir_type) {
                    Console.WriteLine("    type: folder path (entirely/recursively)");
                } else {
                    Console.WriteLine("    type: single file path");
                }
                Console.WriteLine("    preservation archive target:");
                Console.WriteLine("      from: " + absolute_local);
                Console.WriteLine("      to:   " + backup_dest);
                Console.WriteLine("    active pull sync target:");
                Console.WriteLine("      from: " + resolved_item.system_path);
                Console.WriteLine("      to:   " + absolute_local);
                Console.WriteLine();
            }

            Console.Write("do you want to proceed with the pull operation? (y/n): ");
            string pull_confirmation = Console.ReadLine();
            if (string.IsNullOrEmpty(pull_confirmation) || pull_confirmation.Trim().ToLower() != "y") {
                Console.WriteLine("operation denied and aborted");
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
                    CopyDirectory(absolute_local, absolute_local, backup_dest, resolved_item.filters);
                }
            }

            foreach (ResolvedPath resolved_item in flat_list) {
                string absolute_local = Path.Combine(script_dir, resolved_item.local_destination);

                if (resolved_item.is_dir_type) {
                    if (Directory.Exists(resolved_item.system_path)) {
                        if (Directory.Exists(absolute_local)) {
                            Directory.Delete(absolute_local, true);
                        }
                        CopyDirectory(resolved_item.system_path, resolved_item.system_path, absolute_local, resolved_item.filters);
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
            // display planned synchronization map before execution
            Console.WriteLine("execution preview for +push:");
            foreach (ResolvedPath resolved_item in flat_list) {
                string absolute_local = Path.Combine(script_dir, resolved_item.local_destination);
                string preview_push_dir = Path.Combine(script_dir, "last_push");
                string backup_dest = Path.Combine(preview_push_dir, resolved_item.local_destination);

                Console.WriteLine("  target name: " + resolved_item.local_destination);
                if (resolved_item.is_dir_type) {
                    Console.WriteLine("    type: folder path (entirely/recursively)");
                } else {
                    Console.WriteLine("    type: single file path");
                }
                Console.WriteLine("    preservation archive target:");
                Console.WriteLine("      from: " + resolved_item.system_path);
                Console.WriteLine("      to:   " + backup_dest);
                Console.WriteLine("    active push sync target:");
                Console.WriteLine("      from: " + absolute_local);
                Console.WriteLine("      to:   " + resolved_item.system_path);
                Console.WriteLine();
            }

            Console.Write("do you want to proceed with the push operation? (y/n): ");
            string push_confirmation = Console.ReadLine();
            if (string.IsNullOrEmpty(push_confirmation) || push_confirmation.Trim().ToLower() != "y") {
                Console.WriteLine("operation denied and aborted");
                Environment.Exit(1);
            }

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
                    CopyDirectory(resolved_item.system_path, resolved_item.system_path, backup_dest, resolved_item.filters);
                }
            }

            foreach (ResolvedPath resolved_item in flat_list) {
                string absolute_local = Path.Combine(script_dir, resolved_item.local_destination);

                if (resolved_item.is_dir_type) {
                    if (Directory.Exists(absolute_local)) {
                        if (Directory.Exists(resolved_item.system_path)) {
                            Directory.Delete(resolved_item.system_path, true);
                        }
                        CopyDirectory(absolute_local, absolute_local, resolved_item.system_path, resolved_item.filters);
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

    static bool IsPathAllowed(string relative_path, List<FilterBlock> blocks) {
        if (blocks == null) {
            return true;
        }
        if (blocks.Count == 0) {
            return true;
        }

        string norm_path = relative_path.Replace('\\', '/');
        
        FilterBlock winning_block = blocks[0];
        int longest_prefix_length = -1;

        foreach (FilterBlock block in blocks) {
            if (block.block_prefix == "") {
                continue;
            }
            string match_prefix = block.block_prefix + "/";
            if (norm_path.StartsWith(match_prefix, StringComparison.OrdinalIgnoreCase)) {
                int prefix_len = block.block_prefix.Length;
                if (prefix_len > longest_prefix_length) {
                    longest_prefix_length = prefix_len;
                    winning_block = block;
                }
            }
        }

        bool match_found = false;
        foreach (string rule in winning_block.rule_paths) {
            if (rule.EndsWith("/")) {
                if (norm_path.StartsWith(rule, StringComparison.OrdinalIgnoreCase)) {
                    match_found = true;
                    break;
                }
            } else {
                if (string.Equals(norm_path, rule, StringComparison.OrdinalIgnoreCase)) {
                    match_found = true;
                    break;
                }
            }
        }

        if (winning_block.block_directive == "includes_filter") {
            return match_found;
        }

        return !match_found;
    }

    static void CopyDirectory(string source_root, string current_dir, string dest_root, List<FilterBlock> filters) {
        Directory.CreateDirectory(dest_root);
        string[] files = Directory.GetFiles(current_dir);

        foreach (string file in files) {
            int root_len = source_root.Length;
            string relative_path = file.Substring(root_len).TrimStart(Path.DirectorySeparatorChar);
            
            bool allowed = IsPathAllowed(relative_path, filters);
            if (!allowed) {
                continue;
            }
            string file_name = Path.GetFileName(file);
            string dest_file = Path.Combine(dest_root, file_name);
            File.Copy(file, dest_file, true);
        }

        string[] sub_dirs = Directory.GetDirectories(current_dir);
        foreach (string sub_dir in sub_dirs) {
            int root_len = source_root.Length;
            string relative_path = sub_dir.Substring(root_len).TrimStart(Path.DirectorySeparatorChar);
            
            bool allowed = IsPathAllowed(relative_path + "/", filters);
            if (!allowed) {
                continue;
            }
            string dir_name = Path.GetFileName(sub_dir);
            string dest_sub_dir = Path.Combine(dest_root, dir_name);
            CopyDirectory(source_root, sub_dir, dest_sub_dir, filters);
        }
    }
}