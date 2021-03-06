﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.ServiceModel;
using System.Net;
using System.Windows.Input;
using EvlWatcherConsole.MVVMBase;

namespace EvlWatcherConsole
{
    public class EvlWatcherController : GuiObject
    {
        #region private members

        private Thread _updater = null;
        private volatile bool _run = true;
        private object syncObject = new object();

        private int _lockTime = -1;
        private int _timeFrame = -1;
        private int _triggerCount = -1;
        private int _permaBanTrigger = -1;

        private bool _isRunning = false;
        private ObservableCollection<IPAddress> _temporarilyBannedIps = new ObservableCollection<IPAddress>();
        private ObservableCollection<IPAddress> _permanentlyBannedIps = new ObservableCollection<IPAddress>();
        private ObservableCollection<string> _whiteListPattern = new ObservableCollection<string>();

        private string _permaBanIPString = "";
        private string _whiteListFilter = "";

        #endregion

        #region constructor / destructor

        public EvlWatcherController()
        {
            StartUpdating();
        }

        ~EvlWatcherController()
        {
            StopUpdating();
        }

        #endregion

        #region private operations

        private void AddWhiteListEntry(string s)
        {
            lock (syncObject)
            {
                ChannelFactory<WCF.IEvlWatcherService> f = new ChannelFactory<WCF.IEvlWatcherService>(new NetNamedPipeBinding(), new EndpointAddress("net.pipe://localhost/EvlWatcher"));
                WCF.IEvlWatcherService service = f.CreateChannel();
                service.AddWhiteListEntry(s);
            }
        }

        private void RemoveWhiteListEntry(string s)
        {
            lock (syncObject)
            {
                ChannelFactory<WCF.IEvlWatcherService> f = new ChannelFactory<WCF.IEvlWatcherService>(new NetNamedPipeBinding(), new EndpointAddress("net.pipe://localhost/EvlWatcher"));
                WCF.IEvlWatcherService service = f.CreateChannel();
                service.RemoveWhiteListEntry(s);
            }
        }

        private void AddPermanentIPBan(IPAddress a)
        {
            lock (syncObject)
            {
                ChannelFactory<WCF.IEvlWatcherService> f = new ChannelFactory<WCF.IEvlWatcherService>(new NetNamedPipeBinding(), new EndpointAddress("net.pipe://localhost/EvlWatcher"));
                WCF.IEvlWatcherService service = f.CreateChannel();
                service.SetPermanentBan(a);
            }
        }

        private void RemovePermanentIPBan(IPAddress a)
        {
            lock (syncObject)
            {
                ChannelFactory<WCF.IEvlWatcherService> f = new ChannelFactory<WCF.IEvlWatcherService>(new NetNamedPipeBinding(), new EndpointAddress("net.pipe://localhost/EvlWatcher"));
                WCF.IEvlWatcherService service = f.CreateChannel();
                service.ClearPermanentBan(a);
            }
        }

        private void StartUpdating()
        {
            _updater = new Thread(new ThreadStart(this.Run));
            _updater.IsBackground = true;
            _updater.Start();
            _run = true;
        }

        private void StopUpdating()
        {
            try
            {
                _run = false;
                _updater.Interrupt();
                while (_updater.IsAlive)
                    System.Threading.Thread.Sleep(100);
            }
            catch
            { }
        }

        private void Run()
        {
            while (_run)
            {
                

                lock (syncObject)
                {
                    //do not update in design mode
                    
                    bool running = false;

                    try
                    {
                        ChannelFactory<WCF.IEvlWatcherService> f = new ChannelFactory<WCF.IEvlWatcherService>(new NetNamedPipeBinding(), new EndpointAddress("net.pipe://localhost/EvlWatcher"));
                        WCF.IEvlWatcherService service = f.CreateChannel();

                        running = service.GetIsRunning();

                        if (!running)
                            continue;

                        UpdateIPLists(service);
                        UpdateWhileListPattern(service);

                        int lt = service.GetTaskProperty("BlockRDPBruters", "LockTime");
                        int tf = service.GetTaskProperty("BlockRDPBruters", "TimeFrame");
                        int tc = service.GetTaskProperty("BlockRDPBruters", "TriggerCount");
                        int pb = service.GetTaskProperty("BlockRDPBruters", "PermaBanCount");

                        if (lt != _lockTime)
                        {
                            LockTime = lt;
                        }

                        if (tf != _timeFrame)
                        {
                            TimeFrame = tf;
                        }

                        if (tc != _triggerCount)
                        {
                            TriggerCount = tc;
                        }

                        if (pb != _permaBanTrigger)
                        {
                            PermaBanCount = pb;
                        }
                        
                        f.Close();
                    }
                    catch (EndpointNotFoundException)
                    {
                        //service seems not to be running
                    }
                    catch (TimeoutException)
                    {
                        // same here.. well, would be nice if exception filters would have been invented by now...
                    }
                    finally
                    {
                        this.IsRunning = running;
                    }
                }
                try
                {
                    System.Threading.Thread.Sleep(1000);
                }
                catch (ThreadInterruptedException)
                { }
            }
        }

        private void UpdateWhileListPattern(WCF.IEvlWatcherService service)
        {
            List<string> entries = new List<string>(service.GetWhiteListEntries());
            List<string> toAdd = new List<string>();
            List<string> toRemove = new List<string>();

            foreach (string s in entries)
            {
                if (!_whiteListPattern.Contains(s))
                    toAdd.Add(s);
            }
            foreach (string s in _whiteListPattern)
            {
                if (!entries.Contains(s))
                    toRemove.Add(s);
            }
            foreach (string s in toAdd)
                Application.Current.Dispatcher.Invoke(new Action(() => _whiteListPattern.Add(s)));

            foreach (string s in toRemove)
                Application.Current.Dispatcher.Invoke(new Action(() => _whiteListPattern.Remove(s)));
        }

        private void UpdateIPLists(WCF.IEvlWatcherService service)
        {
            List<IPAddress> ips = new List<IPAddress>(service.GetTemporarilyBannedIPs());
            List<IPAddress> toAdd = new List<IPAddress>();
            List<IPAddress> toRemove = new List<IPAddress>();

            foreach (IPAddress i in ips)
            {
                if (!_temporarilyBannedIps.Contains(i))
                    toAdd.Add(i);
            }
            foreach (IPAddress i in _temporarilyBannedIps)
            {
                if (!ips.Contains(i))
                    toRemove.Add(i);
            }
            foreach (IPAddress i in toAdd)
                Application.Current.Dispatcher.Invoke(new Action(() => _temporarilyBannedIps.Add(i)));

            foreach (IPAddress i in toRemove)
                Application.Current.Dispatcher.Invoke(new Action(() => _temporarilyBannedIps.Remove(i)));

            ips = new List<IPAddress>(service.GetPermanentlyBannedIPs());
            toAdd = new List<IPAddress>();
            toRemove = new List<IPAddress>();
            foreach (IPAddress i in ips)
            {
                if (!_permanentlyBannedIps.Contains(i))
                    toAdd.Add(i);
            }
            foreach (IPAddress i in _permanentlyBannedIps)
            {
                if (!ips.Contains(i))
                    toRemove.Add(i);
            }
            foreach (IPAddress i in toAdd)
                Application.Current.Dispatcher.Invoke(new Action(() => _permanentlyBannedIps.Add(i)));

            foreach (IPAddress i in toRemove)
                Application.Current.Dispatcher.Invoke(new Action(() => _permanentlyBannedIps.Remove(i)));
        }

        #endregion

        #region public properties

        public ICommand MoveTemporaryToPermaCommand
        {
            get
            {
                return new RelayCommand(p => { AddPermanentIPBan(SelectedTemporaryIP); }, p => { return SelectedTemporaryIP != null; });
            }
        }

        public ICommand MoveTemporaryToWhiteListCommand
        {
            get
            {
             return new RelayCommand(p => { AddWhiteListEntry(SelectedTemporaryIP.ToString()); }, p => { return SelectedTemporaryIP != null; });
            }
        }

        public IPAddress SelectedTemporaryIP
        {
            get;
            set;
        }

        public IPAddress SelectedPermanentIP
        {
            get;
            set;
        }

        public string SelectedWhiteListPattern
        {
            get;
            set;
        }

        public ICommand AddPermaBanCommand
        {
            get
            {
                return new RelayCommand( p =>
                    { AddPermanentIPBan(IPAddress.Parse(PermaBanIPString)); PermaBanIPString = ""; }, p => { IPAddress dummy; return IPAddress.TryParse(PermaBanIPString, out dummy); });
            }
        }

        public ICommand RemovePermaBanCommand
        {
            get
            {
                return new RelayCommand(p => RemovePermanentIPBan(SelectedPermanentIP), p => { return SelectedPermanentIP != null; });
            }
        }

        public string WhiteListFilter
        {
            get
            {
                return _whiteListFilter;
            }

            set
            {
                _whiteListFilter = value;
                Notify("WhiteListFilter");
            }
        }

        public ICommand AddWhiteListFilterCommand
        {
            get
            {
                //TODO create execute predicate
                return new RelayCommand(p => { AddWhiteListEntry(WhiteListFilter); WhiteListFilter = ""; }, p => { return WhiteListFilter.Length > 0; });
            }
        }

        public ICommand RemoveWhiteListFilterCommand
        {
            get
            {
                return new RelayCommand(p => { RemoveWhiteListEntry(SelectedWhiteListPattern); }, p => { return SelectedWhiteListPattern != null; });
            }
        }

        public string PermaBanIPString
        {
            get
            {
                return _permaBanIPString;
            }

            set
            {
                _permaBanIPString = value;
                Notify("PermaBanIPString");
            }
        }

        public int PermaBanCount
        {
            get
            {
                return _permaBanTrigger;
            }
            set
            {
                lock (syncObject)
                {
                    ChannelFactory<WCF.IEvlWatcherService> f = new ChannelFactory<WCF.IEvlWatcherService>(new NetNamedPipeBinding(), new EndpointAddress("net.pipe://localhost/EvlWatcher"));
                    WCF.IEvlWatcherService service = f.CreateChannel();
                    service.SetTaskProperty("BlockRDPBruters", "PermaBanCount", value);
                    _permaBanTrigger = value;
                    Notify("PermaBanCount");
                }
            }
        }

        public int TriggerCount
        {
            get
            {
                return _triggerCount;
            }
            set
            {
                lock (syncObject)
                {
                    ChannelFactory<WCF.IEvlWatcherService> f = new ChannelFactory<WCF.IEvlWatcherService>(new NetNamedPipeBinding(), new EndpointAddress("net.pipe://localhost/EvlWatcher"));
                    WCF.IEvlWatcherService service = f.CreateChannel();
                    service.SetTaskProperty("BlockRDPBruters", "TriggerCount", value);
                    _triggerCount = value;
                    Notify("TriggerCount");
                }
            }
        }

        public int TimeFrame
        {
            get
            {
                return _timeFrame;
            }
            set
            {
                lock (syncObject)
                {
                    ChannelFactory<WCF.IEvlWatcherService> f = new ChannelFactory<WCF.IEvlWatcherService>(new NetNamedPipeBinding(), new EndpointAddress("net.pipe://localhost/EvlWatcher"));
                    WCF.IEvlWatcherService service = f.CreateChannel();
                    service.SetTaskProperty("BlockRDPBruters", "TimeFrame", value);
                    _timeFrame = value;
                    Notify("TimeFrame");
                }
            }
        }

        public int LockTime
        {
            get
            {
                return _lockTime;
            }
            set
            {
                lock (syncObject)
                {
                    ChannelFactory<WCF.IEvlWatcherService> f = new ChannelFactory<WCF.IEvlWatcherService>(new NetNamedPipeBinding(), new EndpointAddress("net.pipe://localhost/EvlWatcher"));
                    WCF.IEvlWatcherService service = f.CreateChannel();
                    service.SetTaskProperty("BlockRDPBruters", "LockTime", value);
                    _lockTime = value;
                    Notify("LockTime");
                }
            }
        }

        public bool IsRunning
        {
            get
            {
                return _isRunning;
            }

            private set
            {
                _isRunning = value;
                Notify("IsRunning");
            }
        }

        public ObservableCollection<IPAddress> TemporarilyBannedIPs
        {
            get
            {
                return _temporarilyBannedIps;
            }
        }
        public ObservableCollection<IPAddress> PermanentlyBannedIPs
        {
            get
            {
                return _permanentlyBannedIps;
            }
        }
        public ObservableCollection<string> WhiteListedIPs
        {
            get
            {
                return _whiteListPattern;
            }
        }

        #endregion
    }
}
