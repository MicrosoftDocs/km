echo "Creating Azure resources..."
uuid=$(cat /proc/sys/kernel/random/uuid)
clean_uuid=${uuid//[-]/}
location=$1
echo "Creating resource group..."
az group create --name rg${clean_uuid:0:18} --location $location  --output none
echo "Creating storage..."
az storage account create --name store${clean_uuid:0:18} --resource-group rg${clean_uuid:0:18} --location $location --sku Standard_LRS --encryption-services blob --output none
conn=$(az storage account show-connection-string --resource-group rg${clean_uuid:0:18} --name store${clean_uuid:0:18})
set -- $conn
az storage container create --account-name store${clean_uuid:0:18} --name margies --public-access blob --connection-string $3 --output none 
echo "Uploading files..."
az storage blob upload-batch -d margies -s ../data --account-name store${clean_uuid:0:18} --auth-mode key --connection-string $3 --output none 
echo "Creating search service..."
az search service create --name search${clean_uuid:0:18} --resource-group rg${clean_uuid:0:18} --location $location --sku basic --output none
akey=$(az search admin-key show --resource-group rg${clean_uuid:0:18} --service-name search${clean_uuid:0:18})
qkey=$(az search query-key list --resource-group rg${clean_uuid:0:18} --service-name search${clean_uuid:0:18})
echo "-------------------------------------"
echo "Resource Group: rg${clean_uuid:0:18}"
echo "Storage account: store${clean_uuid:0:18}: $conn"
echo "Search Service: search${clean_uuid:0:18}.search.windows.net {"
set -- $akey
echo " \"adminKey\": $3"
set -- $qkey
echo " \"queryKey\": $4"
echo "}"
# Create reset script
echo -e "az login --output none\necho \"Deleting resource group rg${clean_uuid:0:18}\"\naz group delete --name rg${clean_uuid:0:18} --yes\necho \"Resource group rg${clean_uuid:0:18} deleted!\"" > reset.sh
