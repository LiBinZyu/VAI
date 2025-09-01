#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
VAI JointBERT 数据转换工具
将sentenses格式转换为VAI格式
"""

import json
import os
import re
import sys
from typing import Dict, Any


class VAIDataProcessor:
    """VAI数据处理器"""
    
    def __init__(self):
        # 基于functionRegistryExample.json的意图识别模式
        self.intent_patterns = {
            "ChangeObjectColor": ["颜色", "color", "变成", "上色"],
            "ModifyTransform": ["移动", "旋转", "缩放", "变", "调整", "挪", "位移", "挪动", "位置", "移动到", "移"]
        }
        
        # 对象识别模式
        self.object_patterns = {
            "cube": ["方块", "立方体", "正方体", "cube"],
            "sphere": ["球", "球体", "圆球", "sphere"],
            "capsule": ["胶囊", "capsule"],
            "main camera": ["镜头", "摄像机"]
        }
        
        # 变换类型识别模式
        self.transform_patterns = {
            "moveleft": ["向左", "往左", "左", "左边", "左移", "往左边"],
            "moveright": ["向右", "往右", "右", "右边", "右移", "往右边"],
            "moveforward": ["向前", "往前", "前", "前面", "前边", "前边儿"],
            "movebackward": ["向后", "往后", "后面", "后", "后边", "后边儿"],
            "moveup": ["向上", "往上", "上边", "上移", "上"],
            "movedown": ["向下", "往下", "下"],
            "pitch": ["俯仰", "抬头", "低头"],
            "yaw": ["偏航", "转向", "左右转"],
            "roll": ["翻滚", "侧倾", "旋转"],
            "scale": ["放大", "缩小", "变大", "变小"]
        }
        
        # 颜色识别模式
        self.color_patterns = {
            "#FF0000": ["红色", "红"],
            "#00FF00": ["绿色", "绿"],
            "#0000FF": ["蓝色", "蓝"],
            "#FFFF00": ["黄色", "黄"],
            "#00FFFF": ["青色", "青", "蓝绿"],
            "#FF00FF": ["紫色", "紫", "品红"],
            "#FFFFFF": ["白色", "白"],
            "#000000": ["黑色", "黑"],
            "#FFA500": ["橙色", "橙"],
            "#A52A2A": ["棕色", "褐色", "棕"]
        }
    
    def extract_intent(self, text: str) -> str:
        """从文本中提取意图"""
        for intent, patterns in self.intent_patterns.items():
            if any(pattern in text for pattern in patterns):
                return intent
        return "其他"
    
    def extract_entities(self, text: str) -> tuple[Dict[str, str], Dict[str, list]]:
        """从文本中提取实体和位置"""
        entities = {}
        positions = {}
        
        # 提取对象名称
        for obj_value, keywords in self.object_patterns.items():
            for keyword in keywords:
                if keyword in text:
                    start = text.find(keyword)
                    entities["objectName"] = obj_value
                    positions["objectName"] = [start, start + len(keyword)]
                    break
            if "objectName" in entities:
                break
        
        # 提取变换类型
        for transform_value, keywords in self.transform_patterns.items():
            for keyword in keywords:
                if keyword in text:
                    start = text.find(keyword)
                    entities["transformType"] = transform_value
                    positions["transformType"] = [start, start + len(keyword)]
                    break
            if "transformType" in entities:
                break
        
        # 提取颜色
        for color_value, keywords in self.color_patterns.items():
            for keyword in keywords:
                if keyword in text:
                    start = text.find(keyword)
                    entities["hexColor"] = color_value
                    positions["hexColor"] = [start, start + len(keyword)]
                    break
            if "hexColor" in entities:
                break
        
        # 提取数值
        number_pattern = r'(\d+(?:\.\d+)?)'
        match = re.search(number_pattern, text)
        if match:
            entities["number"] = match.group()
            positions["number"] = [match.start(), match.end()]
        
        return entities, positions
    
    def convert_sentence_to_vai(self, text: str, index: int) -> Dict[str, Any]:
        """将单个句子转换为VAI格式"""
        intent = self.extract_intent(text)
        entities, positions = self.extract_entities(text)
        
        # 根据functionRegistryExample.json的格式构建结果
        result = {
            "intent": intent,
            "text": text,
            "slots": entities,
            "positions": positions
        }
        
        # 如果是ModifyTransform意图，确保有正确的参数结构
        if intent == "ModifyTransform":
            if "objectName" not in entities:
                entities["objectName"] = "cube"  # 默认值
            if "transformType" not in entities:
                entities["transformType"] = "moveup"  # 默认值
            if "number" not in entities:
                entities["number"] = "1"  # 默认值
        
        # 如果是ChangeObjectColor意图，确保有正确的参数结构
        elif intent == "ChangeObjectColor":
            if "objectName" not in entities:
                entities["objectName"] = "cube"  # 默认值
            if "hexColor" not in entities:
                entities["hexColor"] = "#FF0000"  # 默认红色
        
        return result
    
    def process_dev_file(self, input_file: str, output_file: str) -> None:
        """处理开发数据文件，保持简单格式"""
        print(f"📖 读取文件: {input_file}")
        
        try:
            with open(input_file, 'r', encoding='utf-8') as f:
                sentences_data = json.load(f)
        except FileNotFoundError:
            print(f"❌ 文件不存在: {input_file}")
            return
        except json.JSONDecodeError as e:
            print(f"❌ JSON解析错误: {e}")
            return
        
        print(f"✅ 成功读取 {len(sentences_data)} 条数据")
        
        vai_data = {}
        
        print(f"🔄 开始转换开发数据...")
        for i, item in enumerate(sentences_data):
            if "text" not in item:
                print(f"⚠️ 跳过第 {i} 条数据，缺少text字段")
                continue
                
            text = item["text"]
            vai_data[str(i)] = {"text": text}
            
            if (i + 1) % 5 == 0:  # 每5条显示一次进度
                print(f"  已处理 {i + 1}/{len(sentences_data)} 条数据")
        
        print(f"💾 保存到文件: {output_file}")
        try:
            with open(output_file, 'w', encoding='utf-8') as f:
                json.dump(vai_data, f, ensure_ascii=False, indent=2)
            print(f"✅ 转换完成！共处理 {len(vai_data)} 条数据")
        except Exception as e:
            print(f"❌ 保存文件失败: {e}")
    
    def process_sentences_file(self, input_file: str, output_file: str) -> None:
        """处理sentences格式文件并转换为VAI格式"""
        print(f"📖 读取文件: {input_file}")
        
        try:
            with open(input_file, 'r', encoding='utf-8') as f:
                sentences_data = json.load(f)
        except FileNotFoundError:
            print(f"❌ 文件不存在: {input_file}")
            return
        except json.JSONDecodeError as e:
            print(f"❌ JSON解析错误: {e}")
            return
        
        print(f"✅ 成功读取 {len(sentences_data)} 条数据")
        
        vai_data = {}
        
        print(f"🔄 开始转换数据...")
        for i, item in enumerate(sentences_data):
            if "text" not in item:
                print(f"⚠️ 跳过第 {i} 条数据，缺少text字段")
                continue
                
            text = item["text"]
            vai_item = self.convert_sentence_to_vai(text, i)
            vai_data[str(i)] = vai_item
            
            if (i + 1) % 5 == 0:  # 每5条显示一次进度
                print(f"  已处理 {i + 1}/{len(sentences_data)} 条数据")
        
        print(f"💾 保存到文件: {output_file}")
        try:
            with open(output_file, 'w', encoding='utf-8') as f:
                json.dump(vai_data, f, ensure_ascii=False, indent=2)
            print(f"✅ 转换完成！共处理 {len(vai_data)} 条数据")
        except Exception as e:
            print(f"❌ 保存文件失败: {e}")
    
    def clean_existing_vai_files(self) -> None:
        """清理现有的VAI格式文件"""
        vai_files = ["data/VAI_train.json", "data/VAI_dev.json"]
        
        for file_path in vai_files:
            if os.path.exists(file_path):
                print(f"🗑️ 删除现有文件: {file_path}")
                os.remove(file_path)
    
    def process_all_data(self) -> None:
        """处理所有数据文件"""
        print(f"{'='*50}")
        print(f"🚀 VAI JointBERT 数据转换工具")
        print(f"{'='*50}")
        
        # 清理现有文件
        self.clean_existing_vai_files()
        
        # 处理训练数据
        if os.path.exists("data/sentenses_train.json"):
            print(f"\n📚 处理训练数据...")
            self.process_sentences_file("data/sentenses_train.json", "data/VAI_train.json")
        else:
            print(f"❌ 未找到训练数据文件: data/sentenses_train.json")
        
        # 处理开发数据 - 保持简单格式
        if os.path.exists("data/sentenses_dev.json"):
            print(f"\n📚 处理开发数据...")
            self.process_dev_file("data/sentenses_dev.json", "data/VAI_dev.json")
        else:
            print(f"❌ 未找到开发数据文件: data/sentenses_dev.json")
        
        print(f"\n{'='*50}")
        print(f"🎉 所有数据处理完成！")
        print(f"{'='*50}")


def main():
    """主函数"""
    if len(sys.argv) > 1 and sys.argv[1] == "--all":
        processor = VAIDataProcessor()
        processor.process_all_data()
    else:
        print("使用方法:")
        print("  python src/data_processor.py --all")
        print("  (处理所有数据文件)")


if __name__ == "__main__":
    main()
