阿里云百炼官方提供了 Python 与 Java 编程语言的 SDK，也提供了与 OpenAI 兼容的调用方式（OpenAI 官方提供了 Python、Node.js、Java、Go 等 SDK）。本文为您介绍如何安装 OpenAI SDK 以及 DashScope SDK。

**环境准备**
--------

若您已熟悉并配置了本地开发环境（Python、Java、Node.js或Go），则可以跳过环境准备阶段，直接[安装SDK](#0c4d002e002d0)。

**环境准备详细配置**

Python

Java

Node.js

Go

### **步骤一：检查您的Python版本**

您可以在终端中输入以下命令查看当前计算环境是否安装了Python和pip：

您的Python需要为3.8或以上版本，请您参考[安装Python](https://help.aliyun.com/zh/sdk/developer-reference/installing-python)进行安装。

    python -V
    pip --version

以Windows的CMD为例：

![image](https://help-static-aliyun-doc.aliyuncs.com/assets/img/zh-CN/2419779371/p914717.png)

#### **常见问题**

Q：执行`python -V`、`pip --version`报错：

*   `'python' 不是内部或外部命令，也不是可运行的程序或批处理文件。`
    
*   `'pip' 不是内部或外部命令，也不是可运行的程序或批处理文件。`
    
*   `-bash: python: command not found`
    
*   `-bash: pip: command not found`
    

解决办法如下：

Windows系统

Linux、macOS系统

1.  请确认是否已参考[安装Python](https://help.aliyun.com/zh/sdk/developer-reference/installing-python)，在您的计算环境中安装Python，并将python.exe添加至环境变量PATH中。![image](https://help-static-aliyun-doc.aliyuncs.com/assets/img/zh-CN/0405879371/p917218.png)
    
2.  如果已安装了Python并添加了环境变量，仍报此错，请关闭当前终端，重新打开一个新的终端窗口，再进行尝试。
    

1.  请确认是否已参考[安装Python](https://help.aliyun.com/zh/sdk/developer-reference/installing-python)，在您的计算环境中安装的Python。
    
2.  如果已安装Python后，仍报此错，请输入`which python pip`命令查询系统中是否有`python`、`pip`。
    
    *   如果返回如下结果，请关闭当前连接终端，重新打开一个新的终端窗口，再进行尝试。
        
            /usr/bin/python
            /usr/bin/pip
        
    *   如果返回如下结果，则再次输入`which python3 pip3`查询。
        
            /usr/bin/which: no python in (/root/.local/bin:/root/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin)
            /usr/bin/which: no pip in (/root/.local/bin:/root/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin)
        
        如果返回结果如下，则使用`python3 -V`、`pip3 --version`查询版本。
        
            /usr/bin/python3
            /usr/bin/pip3
        

### **步骤二：配置虚拟环境（可选）**

如果您的Python已安装完成，可以创建一个虚拟环境来安装OpenAI Python SDK或DashScope Python SDK，这可以帮助您避免与其它项目发生依赖冲突。

1.  **创建虚拟环境**
    
    您可以运行以下命令，创建一个命名为**.venv**的虚拟环境：
    
        # 如果运行失败，您可以将python替换成python3再运行
        python -m venv .venv
    
2.  **激活虚拟环境**
    
    如果您使用Windows系统，请运行以下命令来激活虚拟环境：
    
        .venv\Scripts\activate
    
    如果您使用macOS或者Linux系统，请运行以下命令来激活虚拟环境：
    
        source .venv/bin/activate
    

### **检查您的Java版本**

您可以在终端运行以下命令：

    java -version
    # （可选）如果使用maven管理和构建java项目，还需确保maven已正确安装到您的开发环境中
    mvn --version

以Windows的CMD为例：

![image](https://help-static-aliyun-doc.aliyuncs.com/assets/img/zh-CN/2419779371/p914723.png)

为了使用 Java SDK，您的 Java 需要在Java 8或以上版本。您可以查看打印信息中的第一行确认Java版本，例如打印信息：`openjdk version "16.0.1" 2021-04-20`表明当前 Java 版本为Java 16。如果您当前计算环境没有 Java，或版本低于 Java 8，请前往[Java下载](https://www.oracle.com/cn/java/technologies/downloads/)进行下载与安装。

### **检查Node.js安装状态**

您可以在终端中输入以下命令查看当前计算环境是否安装了Node.js和npm：

    node -v
    npm -v

以Windows的CMD为例：

![image](https://help-static-aliyun-doc.aliyuncs.com/assets/img/zh-CN/2419779371/p914719.png)

这将打印出您当前Node.js 版本。如果您的环境中没有Node.js，请访问[Node.js官网](https://nodejs.org/en/download/package-manager)进行下载。

### **步骤一：检查您的Go版本**

您可以在终端运行以下命令：

    go version

以Windows的CMD为例：

![image](https://help-static-aliyun-doc.aliyuncs.com/assets/img/zh-CN/1686342471/p931008.png)

这将打印出您当前的Go版本。如果您的环境中没有Go，请访问[Go官网](https://golang.google.cn/doc/install)进行下载安装。

### **步骤二：创建项目并初始化模块**

在终端运行以下命令：

    # 创建项目，请根据实际需要修改文件夹名称及路径
    # 此处为Windows系统示例，假设在D盘创建项目文件夹并进入。其他系统切换路径命令请自行适配。
    mkdir D:\your_project_folder && cd /d D:\your_project_folder
    
    # 初始化模块，example.com为示例，符合该格式即可，无需真实域名
    go mod init example.com/your_project_folder

以Windows的CMD为例：

![image](https://help-static-aliyun-doc.aliyuncs.com/assets/img/zh-CN/1686342471/p931146.png)

**安装SDK**
---------

Python

Java

Node.js

Go

您可以通过OpenAI的Python SDK或DashScope的Python SDK来调用百炼平台上的模型。

安装OpenAI Python SDK

安装DashScope Python SDK

通过运行以下命令安装OpenAI Python SDK：

    # 如果运行失败，您可以将pip替换成pip3再运行
    pip install -U openai

![image](https://help-static-aliyun-doc.aliyuncs.com/assets/img/zh-CN/0405879371/p917092.png)

当终端出现`Successfully installed ... openai-x.x.x`的提示后，表示您已经成功安装OpenAI Python SDK。

通过运行以下命令安装DashScope Python SDK：

    # 如果运行失败，您可以将pip替换成pip3再运行
    pip install -U dashscope

![image](https://help-static-aliyun-doc.aliyuncs.com/assets/img/zh-CN/0405879371/p917093.png)

当终端出现`Successfully installed ... dashscope-x.x.x`的提示后，表示您已经成功安装DashScope Python SDK。

**说明**

如果在安装SDK过程中出现`WARNING: You are using pip version xxx; however, version xxx is available.`提示，此为pip工具版本更新通知，与SDK安装无关，请直接忽略即可。

安装DashScope Java SDK

安装OpenAI Java SDK

您可以参考下文来安装 [DashScope Java SDK](https://mvnrepository.com/artifact/com.alibaba/dashscope-sdk-java)。执行以下命令来添加 Java SDK 依赖，并将 `the-latest-version` 替换为最新的版本号。

XML

Gradle

1.  打开您的Maven项目的`pom.xml`文件。
    
2.  在`<dependencies>`标签内添加以下依赖信息。
    
        <dependency>
            <groupId>com.alibaba</groupId>
            <artifactId>dashscope-sdk-java</artifactId>
            <!-- 请将 'the-latest-version' 替换为最新版本号：https://mvnrepository.com/artifact/com.alibaba/dashscope-sdk-java -->
            <version>the-latest-version</version>
        </dependency>
    
3.  保存`pom.xml`文件。
    
4.  使用Maven命令（如`mvn compile`或`mvn clean install`）来更新项目依赖，这样Maven会自动下载并添加DashScope Java SDK到您的项目中。
    

以Windows的IDEA集成开发环境为例：

![image](https://help-static-aliyun-doc.aliyuncs.com/assets/img/zh-CN/0405879371/p917125.png)

1.  打开您的Gradle项目的`build.gradle`文件。
    
2.  在`dependencies`块内添加以下依赖信息。
    
        dependencies {
            // 请将 'the-latest-version' 替换为最新版本号：https://mvnrepository.com/artifact/com.alibaba/dashscope-sdk-java
            implementation group: 'com.alibaba', name: 'dashscope-sdk-java', version: 'the-latest-version'
        }
    
3.  保存`build.gradle`文件。
    
4.  在命令行中，切换到您的项目根目录，执行以下Gradle命令来更新项目依赖。这将会自动下载并添加DashScope Java SDK到您的项目中。
    
        ./gradlew build --refresh-dependencies
    

以Windows的IDEA集成开发环境为例：

![image](https://help-static-aliyun-doc.aliyuncs.com/assets/img/zh-CN/0405879371/p917168.png)

您可以参考下文来添加 OpenAI Java SDK 依赖。

XML

1.  打开Maven项目的`pom.xml`文件。
    
2.  安装 0.32.0 或更高版本的 OpenAI SDK。在`<dependencies>`标签内添加或更新为以下依赖信息，版本号可以使用0.32.0或更高版本。
    
        <dependency>
            <groupId>com.openai</groupId>
            <artifactId>openai-java</artifactId>
            <version>0.32.0</version>
        </dependency>
    
3.  保存`pom.xml`文件。
    
4.  使用Maven命令（如`mvn compile`或`mvn clean install`）来更新项目依赖，这样Maven会自动下载并添加 OpenAI Java SDK到您的项目中。
    

您可以在终端运行以下命令：

    npm install --save openai
    # 或者
    yarn add openai

**说明**

如果安装失败，您可以通过配置镜像源的方法来完成安装，如：

    npm config set registry https://registry.npmmirror.com/

配置镜像源后，您可以重新运行安装SDK的命令。

![image](https://help-static-aliyun-doc.aliyuncs.com/assets/img/zh-CN/0405879371/p917106.png)

当终端出现`added xx package in xxs`的提示后，表示您已经成功安装OpenAI SDK。您可以使用`npm list openai`查询具体版本信息。

OpenAI 提供了 Go 语言的 SDK，您可以在项目目录下通过以下命令来安装：

    go get github.com/openai/openai-go@v0.1.0-alpha.62

![image](https://help-static-aliyun-doc.aliyuncs.com/assets/img/zh-CN/1686342471/p931147.png)

当终端出现`go: added github.com/openai/openai-go v0.1.0-a1pha.62`的提示后，表示您已经成功安装OpenAI SDK。

**说明**

*   经测试`v0.1.0-alpha.62`较为稳定。
    
*   该 SDK 目前还处于测试阶段。
    
*   如访问服务器超时，可设置阿里云镜像代理：
    
        # 设置阿里云镜像
        go env -w GOPROXY=https://mirrors.aliyun.com/goproxy/,direct
    

**下一步**
-------

在[模型调用](https://help.aliyun.com/zh/model-studio/videos/models/)文档中，运行 OpenAI 兼容或 DashScope SDK 的代码示例。

**相关参考**
--------

OpenAI SDK 支持的模型，请参考[OpenAI](https://help.aliyun.com/zh/model-studio/videos/compatible-with-openai)。

[上一篇：配置API Key到环境变量](/zh/model-studio/configure-api-key-through-environment-variables)[下一篇：对话](/zh/model-studio/chat/)