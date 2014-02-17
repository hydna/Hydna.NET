BUILD=xbuild
NUNIT=nunit-console

CONFIGURATION?=Release

all: net20 net30 net35 net40 net45

net20:
	$(BUILD) /p:Configuration=$(CONFIGURATION) src/Hydna.Net20.sln

net30:
	$(BUILD) /p:Configuration=$(CONFIGURATION) src/Hydna.Net30.sln

net35:
	$(BUILD) /p:Configuration=$(CONFIGURATION) src/Hydna.Net35.sln

net40:
	$(BUILD) /p:Configuration=$(CONFIGURATION) src/Hydna.Net40.sln

net45:
	$(BUILD) /p:Configuration=$(CONFIGURATION) src/Hydna.Net45.sln

unity:
	$(BUILD) /p:Configuration=$(CONFIGURATION) /p:DefineConstants="HYDNA_UNITY" src/Hydna.NetUnity.sln

test: test20 test30 test35 test40 test45

test20: net20
	$(NUNIT) lib/net20/Hydna.Net.Tests.dll

test30: net30
	$(NUNIT) lib/net30/Hydna.Net.Tests.dll

test35: net35
	$(NUNIT) lib/net35/Hydna.Net.Tests.dll

test40: net40
	$(NUNIT) lib/net40/Hydna.Net.Tests.dll

test45: net45
	$(NUNIT) lib/net45/Hydna.Net.Tests.dll

clean:
	rm -rf bin/
	rm -rf lib/