﻿// File: ServiceClientLink.cs
// Project: ROS_C-Sharp
// 
// ROS#
// Eric McCann <emccann@cs.uml.edu>
// UMass Lowell Robotics Laboratory
// 
// Reimplementation of the ROS (ros.org) ros_cpp client in C#.
// 
// Created: 07/08/2013
// Updated: 07/26/2013

#region Using

using System;
using System.Collections;
using Messages;
using String = Messages.std_msgs.String;

#endregion

namespace Ros_CSharp
{
    public class IServiceClientLink
    {
        public Connection connection;
        public IServicePublication parent;
        public bool persistent;


        public bool initialize(Connection conn)
        {
            connection = conn;
            connection.DroppedEvent += onConnectionDropped;
            return true;
        }

        public bool handleHeader(Header header)
        {
            if (!header.Values.Contains("md5sum") || !header.Values.Contains("service") || !header.Values.Contains("callerid"))
            {
                string bbq = "Bogus tcpros header. did not have required elements: md5sum, service, callerid";
                ROS.Error(bbq);
                connection.sendHeaderError(ref bbq);
                return false;
            }
            string md5sum = (string) header.Values["md5sum"];
            string service = (string) header.Values["service"];
            string client_callerid = (string) header.Values["client_callerid"];

            if (header.Values.Contains("persistent") && ((string) header.Values["persistent"] == "1" || (string) header.Values["persistent"] == "true"))
                persistent = true;

            ROS.Debug("Service client [{0}] wants service [{1}] with md5sum [{2}]", client_callerid, service, md5sum);
            IServicePublication isp = ServiceManager.Instance.lookupServicePublication(service);
            if (isp == null)
            {
                string bbq = string.Format("received a tcpros connection for a nonexistent service [{0}]", service);
                ROS.Error(bbq);
                connection.sendHeaderError(ref bbq);
                return false;
            }

            if (isp.md5sum != md5sum && md5sum != "*" && isp.md5sum != "*")
            {
                string bbq = "client wants service " + service + " to have md5sum " + md5sum + " but it has " + isp.md5sum + ". Dropping connection";
                ROS.Error(bbq);
                connection.sendHeaderError(ref bbq);
                return false;
            }

            if (isp.isDropped)
            {
                string bbq = "received a tcpros connection for a nonexistent service [" + service + "]";
                ROS.Error(bbq);
                connection.sendHeaderError(ref bbq);
                return false;
            }

            parent = isp;
            IDictionary m = new Hashtable();
            m["request_type"] = isp.req_datatype;
            m["response_type"] = isp.res_datatype;
            m["type"] = isp.datatype;
            m["md5sum"] = isp.md5sum;
            m["callerid"] = this_node.Name;

            connection.writeHeader(m, onHeaderWritten);

            isp.addServiceClientLink(this);
            return true;
        }

        public virtual void processResponse(string error, bool success)
        {
            String msg = new String(error);
            msg.Serialize();
            byte[] buf = new byte[msg.Serialized.Length + 1];
            buf[0] = (byte) (success ? 0x01 : 0x00);
            msg.Serialized.CopyTo(buf, 1);
            connection.write(buf, (uint) buf.Length, onResponseWritten, true);
        }

        public virtual void processResponse(IRosMessage msg, bool success)
        {
            msg.Serialize();
            byte[] buf = new byte[msg.Serialized.Length + 1];
            buf[0] = (byte) (success ? 0x01 : 0x00);
            msg.Serialized.CopyTo(buf, 1);
            connection.write(buf, (uint) buf.Length, onResponseWritten, true);
        }

        public virtual void drop()
        {
            if (connection.sendingHeaderError)
                connection.DroppedEvent -= onConnectionDropped;
            else
                connection.drop(Connection.DropReason.Destructing);
        }

        private void onConnectionDropped(Connection conn, Connection.DropReason reason)
        {
            if (conn != connection || parent == null) return;
            lock (parent)
            {
                parent.removeServiceClientLink(this);
            }
        }


        public virtual void onRequestLength(Connection conn, ref byte[] buffer, uint size, bool success)
        {
            if (!success) return;

            if (conn != connection || size != 4)
                throw new Exception("Invalid request length read");

            uint len = BitConverter.ToUInt32(buffer, 0);
            if (len > 10000000000)
            {
                ROS.Error("A message over a gigabyte was predicted... stop... being... bad.");
                connection.drop(Connection.DropReason.Destructing);
                return;
            }
            connection.read(len, onRequest);
        }

        public virtual void onRequest(Connection conn, ref byte[] buffer, uint size, bool success)
        {
            if (!success) return;
            if (conn != connection)
                throw new Exception("WRONG CONNECTION!");

            if (parent != null)
                lock (parent)
                {
                    parent.processRequest(ref buffer, size, this);
                }
        }

        public virtual void onHeaderWritten(Connection conn)
        {
            connection.read(4, onRequestLength);
        }

        public virtual void onResponseWritten(Connection conn)
        {
            if (conn != connection)
                throw new Exception("WRONG CONNECTION!");

            if (persistent)
                connection.read(4, onRequestLength);
            else
                connection.drop(Connection.DropReason.Destructing);
        }
    }
}