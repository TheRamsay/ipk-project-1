PROJECT_NAME=ipk-project-1
APP_NAME=App
TEST_NAME=App.Tests
OUTPUTPATH = .

.PHONY: build publish clean

all: publish

build_test:
	@echo "Building $(TEST_NAME)..."
	dotnet build $(PROJECT_NAME)/$(TEST_NAME)/$(TEST_NAME).csproj

test: build_test
	@echo "Testing $(TEST_NAME)..."
	dotnet test $(PROJECT_NAME)/$(TEST_NAME)/$(TEST_NAME).csproj

build_app:
	@echo "Building $(APP_NAME)..."
	dotnet build $(PROJECT_NAME)/$(APP_NAME)/$(APP_NAME).csproj

publish: build_app
	@echo "Publishing $(APP_NAME)..."
	dotnet publish $(PROJECT_NAME)/$(APP_NAME)/$(APP_NAME).csproj -p:PublishSingleFile=true -c Release -r linux-x64 --self-contained false  -o $(OUTPUTPATH)
	@echo "Publishing $(APP_NAME) done."

clean:
	@echo "Cleaning $(PROJECT_NAME) build artifacts..."
	dotnet clean $(PROJECT_NAME)/$(APP_NAME)/$(APP_NAME).csproj
	rm -rf $(PROJECT_NAME)/$(APP_NAME)/bin $(PROJECT_NAME)/$(APP_NAME)/obj
