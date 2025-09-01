#!/bin/bash

# VAI JointBERT 项目环境设置脚本
# 使用方法: bash setup_env.sh

echo "🚀 开始设置 VAI JointBERT 项目环境..."

# 检查Python版本
python_version=$(python3 --version 2>&1 | grep -oE '[0-9]+\.[0-9]+')
echo "📋 检测到Python版本: $python_version"

# 创建虚拟环境
echo "📦 创建虚拟环境..."
python3 -m venv vai_env

# 激活虚拟环境
echo "🔧 激活虚拟环境..."
source vai_env/bin/activate

# 升级pip
echo "⬆️ 升级pip..."
pip install --upgrade pip

# 安装依赖
echo "📚 安装项目依赖..."
pip install -r requirements.txt

# 安装开发依赖
echo "🛠️ 安装开发依赖..."
pip install -e .

echo "✅ 环境设置完成！"
echo ""
echo "📝 使用说明:"
echo "1. 激活虚拟环境: source vai_env/bin/activate"
echo "2. 运行数据处理: python src/data_processor.py"
echo "3. 退出虚拟环境: deactivate"
echo ""
echo "🎉 现在可以开始使用项目了！"
