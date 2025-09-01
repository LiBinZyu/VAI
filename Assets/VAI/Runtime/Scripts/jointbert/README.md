# VAI JointBERT Project

这是一个基于BERT的联合学习项目，用于VAI（Virtual Assistant Intelligence）自然语言处理任务。

## 项目结构

```
jointbert/
├── src/                    # 源代码目录
│   ├── models/            # 模型定义
│   ├── data/              # 数据处理
│   ├── data_processor.py  # 数据转换工具
│   └── utils/             # 工具函数
├── data/                  # 数据文件
│   ├── sentenses_train.json  # 原始训练数据
│   ├── sentenses_dev.json    # 原始开发数据
│   ├── VAI_train.json        # VAI格式训练数据
│   └── VAI_dev.json          # VAI格式开发数据
├── requirements.txt       # Python依赖
├── setup.py              # 项目安装配置
├── setup_env.sh          # 环境设置脚本
└── README.md             # 项目说明
```

## 快速开始

### 1. 环境设置

使用提供的脚本快速设置环境：

```bash
# 运行环境设置脚本
bash setup_env.sh
```

或手动设置：

```bash
# 创建虚拟环境
python3 -m venv vai_env
source vai_env/bin/activate

# 安装依赖
pip install -r requirements.txt
```

### 2. 数据处理

将简单的文本格式转换为VAI格式：

```bash
# 处理所有数据文件
python3 src/data_processor.py --all
```

这将：
- 读取 `data/sentenses_train.json` 和 `data/sentenses_dev.json`
- 转换为VAI格式并保存为 `data/VAI_train.json` 和 `data/VAI_dev.json`

## 数据格式

### 输入格式 (sentenses)
```json
[
    {"text": "把方块向右移动3米"},
    {"text": "让球向上移动5个单位"}
]
```

### 输出格式 (VAI)

#### 训练数据格式 (VAI_train.json)
包含完整的意图识别和实体提取信息：
```json
{
  "0": {
    "intent": "ModifyTransform",
    "text": "把方块向右移动3米",
    "slots": {
      "objectName": "cube",
      "transformType": "moveright",
      "number": "3"
    },
    "positions": {
      "objectName": [1, 3],
      "transformType": [3, 5],
      "number": [7, 8]
    }
  }
}
```

#### 开发数据格式 (VAI_dev.json)
保持简单的文本格式，用于测试：
```json
{
  "0": {
    "text": "把方块向右移动3米"
  },
  "1": {
    "text": "让球向上移动5个单位"
  }
}
```

## 支持的意图类型

基于 `functionRegistryExample.json` 的格式：

- **ModifyTransform**: 修改物体的变换属性，包括移动、旋转和缩放
  - 关键词: "移动"、"旋转"、"缩放"、"变"、"调整"、"挪"、"位移"、"挪动"、"位置"、"移动到"、"移"
- **ChangeObjectColor**: 改变物体的颜色
  - 关键词: "颜色"、"color"、"变成"、"上色"

## 支持的实体类型

### ModifyTransform 参数：
- **objectName**: 对象名称 (cube, sphere, capsule, main camera)
- **transformType**: 变换类型 (moveleft, moveright, moveup, movedown, moveforward, movebackward, pitch, yaw, roll, scale)
- **number**: 数值参数

### ChangeObjectColor 参数：
- **objectName**: 对象名称 (cube, sphere, capsule)
- **hexColor**: 十六进制颜色值 (#FF0000, #00FF00, #0000FF, #FFFF00, #00FFFF, #FF00FF, #FFFFFF, #000000, #FFA500, #A52A2A)

## 开发说明

### 依赖管理

项目使用最小化依赖：
- `pandas`: 数据处理
- `numpy`: 数值计算
- `tqdm`: 进度条（可选）
- `click`: 命令行工具（可选）
- `colorama`: 彩色输出（可选）

### 代码规范

- 使用Python 3.8+
- 遵循PEP 8代码规范
- 使用类型注解
- 添加详细的文档字符串

## 许可证

MIT License
