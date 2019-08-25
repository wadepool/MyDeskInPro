using System;
using Crestron.SimplSharp;                        // For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                     // For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;      // For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;       // For Generic Device Support
using Crestron.SimplSharpPro.DM;
using Crestron.SimplSharpPro.UI;
using Crestron.SimplSharpPro.Fusion;

namespace MyDeskInPro
{
  public class ControlSystem : CrestronControlSystem
  {

    private Tsw560 deskPanel;
    private XpanelForSmartGraphics deskXpanel;
    private HdMd6x24kE deskSwitcher;

    string sixByTwoIP = "192.168.1.15";
    string[] sources = {"Main PC", "Laptop", "BrightSign", "AppleTv", "Video Conference"};

    public ControlSystem() : base()
    {
      try
      {
        Thread.MaxNumberOfUserThreads = 20;

        //Subscribe to the controller events (System, Program, and Ethernet)
        CrestronEnvironment.SystemEventHandler += new SystemEventHandler(ControlSystem_ControllerSystemEventHandler);
        CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(ControlSystem_ControllerProgramEventHandler);
        CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(ControlSystem_ControllerEthernetEventHandler);


      }
      catch (Exception e)
      {
        ErrorLog.Error("Error in the constructor: {0}", e.Message);
      }
    }

    /// <summary>
    /// InitializeSystem - this method gets called after the constructor 
    /// has finished. 
    /// 
    /// Use InitializeSystem to:
    /// * Start threads
    /// * Configure ports, such as serial and verisports
    /// * Start and initialize socket connections
    /// Send initial device configurations
    /// 
    /// Please be aware that InitializeSystem needs to exit quickly also; 
    /// if it doesn't exit in time, the SIMPL#Pro program will exit.
    /// </summary>
    public override void InitializeSystem()
    {
      try
      {

        deskPanel = new Tsw560(0x03, this);
        deskPanel.Name = "Touch Panel";
        deskPanel.OnlineStatusChange += new OnlineStatusChangeEventHandler(PanelOnlineStatusChange);
        deskPanel.SigChange += new SigEventHandler(PanelSigChanges);
        
        deskPanel.ExtenderHardButtonReservedSigs.Use();
        
        deskPanel.ExtenderHardButtonReservedSigs.TurnButton1BackLightOff();
        deskPanel.ExtenderHardButtonReservedSigs.TurnButton2BackLightOff();
        deskPanel.ExtenderHardButtonReservedSigs.TurnButton3BackLightOff();
        deskPanel.ExtenderHardButtonReservedSigs.TurnButton4BackLightOn();
        deskPanel.ExtenderHardButtonReservedSigs.TurnButton5BackLightOn();
        
        deskPanel.Register();

        deskPanel.StringInput[10].StringValue = "Beta - Prog";
        deskPanel.StringInput[11].StringValue = "No Input Selected";

        deskXpanel = new XpanelForSmartGraphics(0x04, this);
        deskXpanel.Name = "Xpanel";
        deskXpanel.OnlineStatusChange += new OnlineStatusChangeEventHandler(PanelOnlineStatusChange);
        deskXpanel.SigChange += new SigEventHandler(PanelSigChanges);
        deskPanel.Register();
        
        deskSwitcher = new HdMd6x24kE(0x12, sixByTwoIP, this);
        deskSwitcher.IpInformationChange += new IpInformationChangeEventHandler(deskSwitcherIpInformationChange);
        deskSwitcher.OnlineStatusChange += new OnlineStatusChangeEventHandler(PanelOnlineStatusChange);
        deskSwitcher.Register();

      }
      catch (Exception e)
      {
        ErrorLog.Error("Error in InitializeSystem: {0}\r\n", e.Message);
      }
    }

    void deskSwitcherIpInformationChange(GenericBase currentDevice, ConnectedIpEventArgs args)
    {
      CrestronConsole.PrintLine("\n IP Information Change:\n  Device: {0}\n  Connected: {1} -> IPA: {2}\r\n", currentDevice, args.Connected, args.DeviceIpAddress);
    }

    void PanelOnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
    {
      ErrorLog.Notice("Checking Status of {0}...", currentDevice.Name);
      if (args.DeviceOnLine)
      {
        ErrorLog.Error("{0} is Online", currentDevice.Name);
      }
      else
      {
        ErrorLog.Error("{0} is Offline!", currentDevice.Name);
      }
    }

    void PanelSigChanges(BasicTriList currentDevice, SigEventArgs args)
    {
      switch (args.Sig.Type)
      {
        // Digital Presses
        case eSigType.Bool:
          if (args.Sig.Number >= 11 && args.Sig.Number <= 15)
          {
            uint inputNum = args.Sig.Number - 10;
            setSwitcher(inputNum, 1, currentDevice);
            interlock(args.Sig.Number, currentDevice);
          } /*
          if (args.Sig.Number == 11)
          {
            uint inputNum = args.Sig.Number - 10;
            setSwitcher(inputNum, 1, currentDevice);
            interlock(args.Sig.Number, currentDevice);
          }
          else if (args.Sig.Number == 12)
          {
            uint inputNum = args.Sig.Number - 10;
            setSwitcher(inputNum, 1, currentDevice);
            interlock(args.Sig.Number, currentDevice);
          }
          else if (args.Sig.Number == 13)
          {
            uint inputNum = args.Sig.Number - 10;
            setSwitcher(inputNum, 1, currentDevice);
            interlock(args.Sig.Number, currentDevice);
          }
          else if (args.Sig.Number == 14)
          {
            uint inputNum = args.Sig.Number - 10;
            setSwitcher(inputNum, 1, currentDevice);
            interlock(args.Sig.Number, currentDevice);
          }
          else if (args.Sig.Number == 15)
          {
            uint inputNum = args.Sig.Number - 10;
            setSwitcher(inputNum, 1, currentDevice);
            interlock(args.Sig.Number, currentDevice);
          } */
          else if (args.Sig.Number == 16)
          {
            clearSwitcher(1);
            ushort[] indexes = { 11, 12, 13, 14, 15 };
            foreach (ushort index in indexes)
            {
              deskPanel.BooleanInput[index].BoolValue = false;
            }
            deskPanel.StringInput[11].StringValue = "No Input Selected";
          }

          break;
        case eSigType.NA:
          break;
        // Serial String
        case eSigType.String:
          break;
        // Analog Values
        case eSigType.UShort:
          break;
        default:
          break;
      }
    }

    public void interlock(uint digiJoin, BasicTriList myXpanel)
    {
      myXpanel.BooleanInput[digiJoin].BoolValue = true;
    }

    public void clearInterlock(ushort startIndex, ushort endIndex, BasicTriList myXpanel)
    {
      for (ushort i = startIndex; i <= endIndex; i++)
      {
        myXpanel.BooleanInput[i].BoolValue = false;
      }
    }

    // To create a clear interlock with non consecutive number, we pass an array
    void resetInterlockArray(ushort[] indexes, BasicTriList myXpanel)
    {
      foreach (ushort index in indexes)
      {
        myXpanel.BooleanInput[index].BoolValue = false;
      }
    }

    void setSwitcher(uint hdmiInput, uint hdmiOuput, BasicTriList device)
    {
      CrestronConsole.Print("\nUsed {0} to Route HDMI {1} to Output {2}\r\n", device, hdmiInput, hdmiOuput);
      clearInterlock(11, 15, device);
      DMOutput outputToSend = deskSwitcher.Outputs[hdmiOuput] as DMOutput;
      outputToSend.VideoOut = deskSwitcher.Inputs[hdmiInput];
      int selectedSource = (int)hdmiInput - 1;
      deskPanel.StringInput[11].StringValue = sources[selectedSource];
    }

    void clearSwitcher(uint hdmiOutput)
    {
      CrestronConsole.Print("\nClearing the route\r\n");
      DMOutput outputToClear = deskSwitcher.Outputs[hdmiOutput] as DMOutput;
      outputToClear.VideoOut = null;
    }

    void sourceSerial(uint source)
    {
      deskPanel.StringInput[11].StringValue = sources[source];
    }
    /// <summary>
    /// Event Handler for Ethernet events: Link Up and Link Down. 
    /// Use these events to close / re-open sockets, etc. 
    /// </summary>
    /// <param name="ethernetEventArgs">This parameter holds the values 
    /// such as whether it's a Link Up or Link Down event. It will also indicate 
    /// wich Ethernet adapter this event belongs to.
    /// </param>
    void ControlSystem_ControllerEthernetEventHandler(EthernetEventArgs ethernetEventArgs)
    {
      switch (ethernetEventArgs.EthernetEventType)
      {//Determine the event type Link Up or Link Down
        case (eEthernetEventType.LinkDown):
          //Next need to determine which adapter the event is for. 
          //LAN is the adapter is the port connected to external networks.
          if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
          {
            //
          }
          break;
        case (eEthernetEventType.LinkUp):
          if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
          {
            //
          }
          break;
      }
    }

    /// <summary>
    /// Event Handler for Programmatic events: Stop, Pause, Resume.
    /// Use this event to clean up when a program is stopping, pausing, and resuming.
    /// This event only applies to this SIMPL#Pro program, it doesn't receive events
    /// for other programs stopping
    /// </summary>
    /// <param name="programStatusEventType"></param>
    void ControlSystem_ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)
    {
      switch (programStatusEventType)
      {
        case (eProgramStatusEventType.Paused):
          //The program has been paused.  Pause all user threads/timers as needed.
          break;
        case (eProgramStatusEventType.Resumed):
          //The program has been resumed. Resume all the user threads/timers as needed.
          break;
        case (eProgramStatusEventType.Stopping):
          //The program has been stopped.
          //Close all threads. 
          //Shutdown all Client/Servers in the system.
          //General cleanup.
          //Unsubscribe to all System Monitor events
          break;
      }

    }

    /// <summary>
    /// Event Handler for system events, Disk Inserted/Ejected, and Reboot
    /// Use this event to clean up when someone types in reboot, or when your SD /USB
    /// removable media is ejected / re-inserted.
    /// </summary>
    /// <param name="systemEventType"></param>
    void ControlSystem_ControllerSystemEventHandler(eSystemEventType systemEventType)
    {
      switch (systemEventType)
      {
        case (eSystemEventType.DiskInserted):
          //Removable media was detected on the system
          break;
        case (eSystemEventType.DiskRemoved):
          //Removable media was detached from the system
          break;
        case (eSystemEventType.Rebooting):
          //The system is rebooting. 
          //Very limited time to preform clean up and save any settings to disk.
          break;
      }

    }
  }
}
