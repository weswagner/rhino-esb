using System;
using System.Collections.Generic;
using System.Configuration;
using Castle.Core;
using Castle.Core.Configuration;
using Castle.MicroKernel;
using Castle.MicroKernel.Registration;
using Rhino.ServiceBus.Actions;
using System.Transactions;
using Rhino.ServiceBus.Msmq;

namespace Rhino.ServiceBus.Impl
{
	public class RhinoServiceBusFacility : AbstractRhinoServiceBusFacility
    {
        protected readonly List<MessageOwner> messageOwners = new List<MessageOwner>();

        protected override void ReadConfiguration()
	    {
	        ReadBusConfiguration();
	        ReadMessageOwners();
	    }

        protected override void RegisterComponents()
        {
            Kernel.Register(
                Component.For<IDeploymentAction>()
                    .ImplementedBy<CreateLogQueueAction>(),
                Component.For<IDeploymentAction>()
                    .ImplementedBy<CreateQueuesAction>(),
                Component.For<IServiceBus, IStartableServiceBus>()
                    .ImplementedBy<DefaultServiceBus>()
                    .LifeStyle.Is(LifestyleType.Singleton)
                    .DependsOn(new
                    {
                        messageOwners = messageOwners.ToArray(),
                    })
                    .Parameters(Parameter.ForKey("modules").Eq(CreateModuleConfigurationNode())
                    )
                );
        }

        private IConfiguration CreateModuleConfigurationNode()
        {
            var config = new MutableConfiguration("array");
            foreach (Type type in messageModules)
            {
                config.CreateChild("item", "${" + type.FullName + "}");
            }
            return config;
        }

        protected void ReadMessageOwners()
        {
            IConfiguration messageConfig = FacilityConfig.Children["messages"];
            if (messageConfig == null)
                throw new ConfigurationErrorsException("Could not find 'messages' node in configuration");

            foreach (IConfiguration configuration in messageConfig.Children)
            {
                if (configuration.Name != "add")
                    throw new ConfigurationErrorsException("Unknown node 'messages/" + configuration.Name + "'");

                string msgName = configuration.Attributes["name"];
                if (string.IsNullOrEmpty(msgName))
                    throw new ConfigurationErrorsException("Invalid name element in the <messages/> element");

                string uriString = configuration.Attributes["endpoint"];
                Uri ownerEndpoint;
                try
                {
                    ownerEndpoint = new Uri(uriString);
                }
                catch (Exception e)
                {
                    throw new ConfigurationErrorsException("Invalid endpoint url: " + uriString, e);
                }

            	bool? transactional = null;
				string transactionalString = configuration.Attributes["transactional"];
				if (string.IsNullOrEmpty(transactionalString) == false)
				{
					bool temp;
					if(bool.TryParse(transactionalString, out temp)==false)
						throw new ConfigurationErrorsException("Invalid transactional settings: " + transactionalString);
					transactional = temp;
				}

            	messageOwners.Add(new MessageOwner
                {
                    Name = msgName,
                    Endpoint = ownerEndpoint,
                    Transactional = transactional
                });
            }
        }

        protected void ReadBusConfiguration()
        {
            IConfiguration busConfig = FacilityConfig.Children["bus"];
            if (busConfig == null)
                throw new ConfigurationErrorsException("Could not find 'bus' node in configuration");

            string retries = busConfig.Attributes["numberOfRetries"];
            int result;
            if (int.TryParse(retries, out result))
                NumberOfRetries = result;

            string threads = busConfig.Attributes["threadCount"];
            if (int.TryParse(threads, out result))
                ThreadCount = result;

        	string isolationLevel = busConfig.Attributes["queueIsolationLevel"];
			if (!string.IsNullOrEmpty(isolationLevel))
				queueIsolationLevel = (IsolationLevel)Enum.Parse(typeof(IsolationLevel), isolationLevel);
			

            string uriString = busConfig.Attributes["endpoint"];
            Uri endpoint;
            if (Uri.TryCreate(uriString, UriKind.Absolute, out endpoint) == false)
            {
                throw new ConfigurationErrorsException(
                    "Attribute 'endpoint' on 'bus' has an invalid value '" + uriString + "'");
            }
            Endpoint = endpoint;

			string transactionalString = busConfig.Attributes["transactional"];
        	bool temp;
			if (bool.TryParse(transactionalString, out temp))
			{
				Transactional = temp ? TransactionalOptions.Transactional : TransactionalOptions.NonTransactional;
			}
			else if(transactionalString != null)
			{
				throw new ConfigurationErrorsException(
					"Attribute 'transactional' on 'bus' has an invalid value '" + uriString + "'");
			}
        }

		public IFacility UseFlatQueueStructure()
	    {
	        UseFlatQueue = true;
	        return this;
	    }
    }
}
