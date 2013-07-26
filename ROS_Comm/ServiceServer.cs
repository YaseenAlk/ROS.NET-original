﻿// File: ServiceServer.cs
// Project: ROS_C-Sharp
// 
// ROS#
// Eric McCann <emccann@cs.uml.edu>
// UMass Lowell Robotics Laboratory
// 
// Reimplementation of the ROS (ros.org) ros_cpp client in C#.
// 
// Created: 03/04/2013
// Updated: 07/26/2013

#region Using

using System;
using System.Diagnostics;

#endregion

namespace Ros_CSharp
{
    public class ServiceServer
    {
        internal double constructed =
            (int) Math.Floor(DateTime.Now.Subtract(Process.GetCurrentProcess().StartTime).TotalMilliseconds);

        internal NodeHandle nodeHandle;
        internal string service = "";
        internal bool unadvertised;

        public ServiceServer(string service, NodeHandle nodeHandle)
        {
            this.service = service;
            this.nodeHandle = nodeHandle;
        }

        public bool IsValid
        {
            get { return !unadvertised; }
        }

        public void shutdown()
        {
            unadvertise();
        }

        public string getService()
        {
            return service;
        }

        internal void unadvertise()
        {
            if (!unadvertised)
            {
                unadvertised = true;
                ServiceManager.Instance.unadvertiseService(service);
            }
        }
    }
}