
using System;
using Microsoft.MetadirectoryServices;
using Microsoft.MetadirectoryServices.Logging; // for text-based logging
using System.Xml; // for reading config file
using System.Data; // for dataTables to 
using System.Collections.Generic; // for lists

namespace Mms_Metaverse
{
    /// <summary>
    /// Summary description for MVExtensionObject.
    /// </summary>
    public class MVExtensionObject : IMVSynchronization
    {
        #region "Global Declarations"
        ConnectedMA maTargetMA;
        CSEntry CSEntry;
        string RDN;
        ReferenceValue targetDN;
        string sMAName;
        List<string> lstMANames = new List<string>();
        DataTable dtMAs; DataRow dtRow; DataColumn dtColumn; DataRow[] dtResultRow; // datatable
        string sTargetOU;
        ReferenceValue targetDN;
        ConnectedMA maTargetMA;
        #endregion

        public MVExtensionObject()
        {

        }

        void IMVSynchronization.Initialize()
        {
            // define our XML configuration file
            string sConfigXML = Utils.ExtensionsDirectory + @"\config.xml";
            XmlDocument xmlConfig = new XmlDocument();
            XmlNode xmlTargets; XmlNode node;

            // string to temporarily hold our mv object type.  it's easiest to use our mv object type and see whether our
            // list of target MAs contain this object type to determine whether to do our outbound provisioning
            string sMVObjectType;
        try
            {
                #region "DataTable schema creation"
                // create schema for the data table to hold XML configuration variables
                dtMAs = new DataTable();
                dtColumn = new DataColumn("MAName", System.Type.GetType("System.String")); dtMAs.Columns.Add(dtColumn);
                dtColumn = new DataColumn("IsTarget", System.Type.GetType("System.String")); dtMAs.Columns.Add(dtColumn);
                dtColumn = new DataColumn("TargetOU", System.Type.GetType("System.String")); dtMAs.Columns.Add(dtColumn);
                dtColumn = new DataColumn("IsEnabled", System.Type.GetType("System.String")); dtMAs.Columns.Add(dtColumn);
                #endregion "DataTable schema creation"

                //Load config.xml
                xmlConfig.Load(sConfigXML);

                //=== Load vars for ADMA ===//
                //xmlProperty = xmlConfig.SelectSingleNode("rules-extension-properties/management-agents/socom-adma/outbound-target-ou");
                //sContactOU = xmlProperty.InnerText; // load our default target OU. this is only used if there was no target OU defined for the contributing MA

                //xmlProperty = xmlConfig.SelectSingleNode("rules-extension-properties/management-agents/ADMA/name");
                //sADMA = xmlProperty.InnerText; // OUR ADMA name

                //xmlProperty = xmlConfig.SelectSingleNode("rules-extension-properties/management-agents/ADMA/enabled");
                //bSocomMAEnabled = Convert.ToBoolean(xmlProperty.InnerText); // whether our ADMA is enabled or not

                /// MISSION PARTNERS AND SUBORDINATE AGENCIES ///
                // for each agency in the config file, cycle through and add to our dataTable

                // our root node for mission partners in the XML file
                xmlTargets = xmlConfig.SelectSingleNode("rules-extension-properties/management-agents/mission-partners");

                foreach (XmlNode nodeTarget in xmlTargets.ChildNodes)
                    {
                        
                        node = nodeTarget.SelectSingleNode("enabled");
                        // if MA is enabled in the config file, then add to the dataTable. if it's disabled then skip to the next node
                        if (Convert.ToBoolean(node.InnerText))
                        {
                            dtRow = dtMAs.NewRow(); //creates new row in provisioning data table

                            // populate columns - note that enabled is out of order since we used it to determine if we're even adding it to the list
                            dtRow["IsEnabled"] = node.InnerText;
                            node = nodeTarget.SelectSingleNode("name"); dtRow["MAName"] = node.InnerText; sMAName = node.InnerText;
                            node = nodeTarget.SelectSingleNode("is-target"); dtRow["IsTarget"] = node.InnerText;
                            node = nodeTarget.SelectSingleNode("target-ou"); dtRow["TargetOU"] = node.InnerText;

                            //load the names list of target MAs
                            lstMANames.add(sMAName);
                }
                        }
                    }   

            }
        catch(Exception ex)
            {

            }  
        }

        void IMVSynchronization.Terminate()
        {
            //
            // TODO: Add termination logic here
            //
        }

        void IMVSynchronization.Provision(MVEntry mventry)
        {
            if (mventry["mail"].IsPresent)
            {
                foreach (string sMA in lstMANames) ProvisionContact(mventry, sMA);
            }
        }
        bool IMVSynchronization.ShouldDeleteFromMV(CSEntry csentry, MVEntry mventry)
        {
            //
            // TODO: Add MV deletion logic here
            //
            throw new EntryPointNotImplementedException();
        }
        private void ProvisionContact(MVEntry mventry, string sMA)
        {
            
            switch (mventry.ObjectType.ToLower())
            {
                //Person - MV Object type to scope provision of our users to the mission partners domain as contact objects. 
                //All outbound flows need to be mapped from person to contact
                #region case "person":
                case "person":
                    {
                        dtResultRow = dtMAs.Select("MAName = '" + sMA + "'"); // retrieve our config data for this MA from the datatable
                        bool bContactsConnected = false; // reset our boolean 
                        bool bProv = false;
                        if (mventry["mail"].IsPresent) bProv = true;
                        maTargetMA = mventry.ConnectedMAs[sMA]; //Declares MA to Provisions

                        int iNumConnectorsContacts = maTargetMA.Connectors.Count; // count our connectors to this MA 

                        if (bProv)
                        {
                            if (iNumConnectorsContacts > 0) bContactsConnected = true;
                            sTargetOU = dtResultRow[0][2].ToString(); // the 6th column in the dataTable holds our target OU structure for outbound MAs
                            RDN = "CN=" + mventry["cn"].Value + ",OU=ExternalContacts" + sTargetOU;
                            targetDN = maTargetMA.CreateDN(RDN); //Created the CS DN
                            if (!(bContactsConnected)) //If not found while iNumConnectorsContacts
                            {
                                CSEntry = maTargetMA.Connectors.StartNewConnector("contact"); //Starts a new connector
                                CSEntry.DN = targetDN; //Sets the CS DN from targetDN
                                CSEntry["targetAddress"].Value = mventry["mail"].Value; //flows mail attribute MV > CS
                                CSEntry.CommitNewConnector(); //commits the connector to cs db
                            }
                        }
                        break;
                    }
                #endregion case "person"

                //GALSyncPerson - MV Object type to scope provision of external contacts from the GALSync.com domain to AD into our domain under the "ExternalContacts" OU
                //For this to work we need to make a copy of the person object type in the MV designer and change its name to 'GalSyncPerson
                //All external inbound flows need to be mapped to this object type
                #region case "GalSyncPerson":
                case galsyncperson":
                    {

                        bool bContactsConnected = false; // reset our boolean 
                        bool bProv = false;
                        if (mventry["mail"].IsPresent) bProv = true;
                        maTargetMA = mventry.ConnectedMAs["ADMA"]; //Declares MA to Provisions

                        int iNumConnectorsContacts = maTargetMA.Connectors.Count; // count our connectors to this MA 

                        if (bProv)
                        {
                            if (iNumConnectorsContacts > 0) bContactsConnected = true;
                            RDN = "CN=" + mventry["cn"].Value + ",OU=ExternalContacts" + ",DC=Contoso,DC=com";
                            targetDN = maTargetMA.CreateDN(RDN); //Created the CS DN
                            if (!(bContactsConnected)) //If not found while iNumConnectorsContacts
                            {
                                CSEntry = maTargetMA.Connectors.StartNewConnector("contact"); //Starts a new connector
                                CSEntry.DN = targetDN; //Sets the CS DN from targetDN
                                CSEntry["targetAddress"].Value = mventry["mail"].Value; //flows mail attribute MV > CS
                                CSEntry.CommitNewConnector(); //commits the connector to cs db
                            }
                        }
                        break;
                    }
                    #endregion case "GalSyncPerson"
            }
        }
    }
}