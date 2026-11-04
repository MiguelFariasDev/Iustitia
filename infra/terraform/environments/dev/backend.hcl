# terraform init -backend-config=environments/dev/backend.hcl
# Storage account criada por scripts/azure-bootstrap.sh (versionamento + soft delete).
resource_group_name  = "rg-advocacia-shared"
storage_account_name = "stadvocaciatfstate"
container_name       = "tfstate"
key                  = "dev.terraform.tfstate"
