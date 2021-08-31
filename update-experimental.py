import os
import json
import time
from zipfile import ZipFile

outputDir = '.\\bin\\x64\\Release'
buildVersion = ''

def buildProject():
    # "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\msbuild.exe" FFTriadBuddyDalamud\TriadBuddy.sln -t:Clean,Restore,Build -p:Configuration=Release
    builderFolder = "C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\Community\\MSBuild\\Current\\Bin"
    os.system('"%s\\msbuild.exe" TriadBuddy.sln -t:Clean,Restore,Build -p:Configuration=Release' % builderFolder);

def processOutputManifest():
    manifestPath = os.path.join(outputDir, 'TriadBuddy.json')
    with open(manifestPath, 'r') as mfile:
        manifestOb = json.load(mfile)

    global buildVersion
    buildVersion = manifestOb['AssemblyVersion']
    print('Using build version: %s' % buildVersion)


def stageBuild():
    myDir = os.getcwd()
    os.chdir(outputDir)
    
    buildZip = 'temp.zip'
    if os.path.exists(buildZip):
        os.remove(buildZip)
        
    with ZipFile(buildZip, 'w') as zipOb:
        for item in os.listdir('.'):
            if os.path.isfile(item) and item != 'temp.zip':
                zipOb.write(item)

    os.chdir(myDir)
    
    buildZip = os.path.join(outputDir, buildZip)
    stagedZip = '.\\experimental\\api4\\latest.zip'
    if os.path.exists(stagedZip):
        os.remove(stagedZip)
    os.rename(buildZip, stagedZip)


def updatePluginMaster():
    manifestPath = '.\\experimental\\pluginmaster.json'
    with open(manifestPath, 'r') as mfile:
        manifestOb = json.load(mfile)

    manifestOb[1]['AssemblyVersion'] = buildVersion
    manifestOb[1]['TestingAssemblyVersion'] = buildVersion
    manifestOb[1]['LastUpdated'] = int(time.time())

    with open(manifestPath, 'w') as mfile:
        mfile.write(json.dumps(manifestOb, indent=4, sort_keys=True))

# run script
buildProject()
processOutputManifest()
stageBuild()
updatePluginMaster()
