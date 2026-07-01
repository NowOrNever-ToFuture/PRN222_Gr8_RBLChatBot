import os
import re
import shutil
import uuid
import asyncio
from functools import lru_cache

import pandas as pd
import torch
from docx import Document as DocxDocument
from fastapi import FastAPI, File, Form, HTTPException, UploadFile
from pydantic import BaseModel
from pptx import Presentation
from pypdf import PdfReader
from sentence_transformers import SentenceTransformer

app = FastAPI(title="PRN222 Local RAG AI Server")

SUPPORTED_MODELS = ["bge-m3", "e5", "phobert"]
loaded_models = {}


@lru_cache(maxsize=1)
def load_qwen_model():
    """Load and cache the Qwen base model with the local LoRA adapter."""
    from peft import PeftModel
    from unsloth import FastLanguageModel

    device_map = "auto" if torch.cuda.is_available() else "cpu"
    model, tokenizer = FastLanguageModel.from_pretrained(
        model_name="unsloth/Qwen2.5-1.5B-Instruct",
        max_seq_length=2048,
        dtype=None,
        load_in_4bit=torch.cuda.is_available(),
        device_map=device_map,
    )
    model = PeftModel.from_pretrained(model, "./qwen_lora")
    FastLanguageModel.for_inference(model)
    return model, tokenizer

def get_model(model_name: str) -> SentenceTransformer:
    if model_name not in SUPPORTED_MODELS:
        raise HTTPException(status_code=400, detail="Mo hinh khong duoc ho tro")
    
    if model_name not in loaded_models:
        print(f"Dang tai mo hinh {model_name} vao bo nho RAM...")
        if model_name == "bge-m3":
            loaded_models[model_name] = SentenceTransformer("BAAI/bge-m3")
        elif model_name == "e5":
            loaded_models[model_name] = SentenceTransformer("intfloat/multilingual-e5-base")
        elif model_name == "phobert":
            loaded_models[model_name] = SentenceTransformer("keepitreal/vietnamese-sbert")
        print(f"Tai mo hinh {model_name} hoan tat!")
        
    return loaded_models[model_name]

# Tai truoc mo hinh mac dinh bge-m3 vao RAM tai startup de tranh bi tre o request dau tien
try:
    print("Dang tai mo hinh mac dinh bge-m3 vao bo nho RAM...")
    loaded_models["bge-m3"] = SentenceTransformer("BAAI/bge-m3")
    print("Tai mo hinh mac dinh hoan tat! Server san sang phuc vu tai Port 8000.")
except Exception as e:
    print(f"Canh bao: Khong the tai truoc mo hinh bge-m3: {e}")


class EmbedRequest(BaseModel):
    text: str
    model_name: str = "bge-m3"


@app.get("/")
async def root():
    return {"message": "Welcome to PRN222 Local RAG AI Server. Visit /docs for API documentation."}


@app.get("/api/health")
async def health_check():
    return {"status": "ok", "models": SUPPORTED_MODELS}


@app.post("/api/embed")
async def generate_embedding(req: EmbedRequest):
    model = get_model(req.model_name)
    vector = await asyncio.to_thread(model.encode, req.text)
    return {"vector": vector.tolist()}


def parse_pdf_to_markdown(file_path: str) -> str:
    reader = PdfReader(file_path)
    markdown_content = []

    for index, page in enumerate(reader.pages):
        text = page.extract_text()
        if text:
            markdown_content.append(f"## --- TRANG {index + 1} ---\n{text}")

    return "\n\n".join(markdown_content)


def parse_docx_to_markdown(file_path: str) -> str:
    doc = DocxDocument(file_path)
    markdown_content = []

    for paragraph in doc.paragraphs:
        text = paragraph.text.strip()
        if not text:
            continue

        style_name = paragraph.style.name
        if style_name.startswith("Heading 1"):
            markdown_content.append(f"# {text}")
        elif style_name.startswith("Heading 2"):
            markdown_content.append(f"## {text}")
        elif style_name.startswith("Heading 3"):
            markdown_content.append(f"### {text}")
        else:
            markdown_content.append(text)

    return "\n\n".join(markdown_content)


def parse_pptx_to_markdown(file_path: str) -> str:
    presentation = Presentation(file_path)
    markdown_content = []

    for index, slide in enumerate(presentation.slides):
        markdown_content.append(f"## --- SLIDE {index + 1} ---")
        for shape in slide.shapes:
            if hasattr(shape, "text") and shape.text.strip():
                markdown_content.append(shape.text.strip())

    return "\n\n".join(markdown_content)


def parse_xlsx_to_markdown(file_path: str) -> str:
    workbook = pd.ExcelFile(file_path)
    markdown_content = []

    for sheet_name in workbook.sheet_names:
        data_frame = pd.read_excel(workbook, sheet_name=sheet_name)
        markdown_content.append(f"## Bang tinh: {sheet_name}")
        markdown_content.append(data_frame.to_markdown(index=False))

    return "\n\n".join(markdown_content)


def chunk_fixed_size(text: str, chunk_size: int = 800, overlap: int = 100) -> list[str]:
    chunks = []
    step = max(chunk_size - overlap, 1)

    for start in range(0, len(text), step):
        chunk = text[start : start + chunk_size].strip()
        if chunk:
            chunks.append(chunk)
        if start + chunk_size >= len(text):
            break

    return chunks


def chunk_by_sentence(text: str, max_chunk_size: int = 800) -> list[str]:
    sentences = [s.strip() for s in re.split(r"(?<=[.!?])\s+|\n+", text) if s.strip()]
    chunks = []
    current_chunk = ""

    for sentence in sentences:
        if len(sentence) > max_chunk_size:
            if current_chunk.strip():
                chunks.append(current_chunk.strip())
                current_chunk = ""
            chunks.extend(chunk_fixed_size(sentence, max_chunk_size, 0))
            continue

        separator = " " if current_chunk else ""
        if len(current_chunk) + len(separator) + len(sentence) > max_chunk_size:
            if current_chunk.strip():
                chunks.append(current_chunk.strip())
            current_chunk = sentence
        else:
            current_chunk += separator + sentence

    if current_chunk.strip():
        chunks.append(current_chunk.strip())

    return chunks


def chunk_markdown(markdown_result: str, chunk_strategy: str, chunk_size: int = 500) -> list[str]:
    chunk_strategy = (chunk_strategy or "markdown_header").lower()
    chunks = []

    if chunk_strategy == "markdown_header":
        raw_sections = markdown_result.split("\n#")
        for section in raw_sections:
            if section.strip():
                chunks.append("#" + section if not section.startswith("#") else section)
        return chunks

    if chunk_strategy == "fixed_size":
        return chunk_fixed_size(markdown_result, chunk_size, 0)

    if chunk_strategy == "fixed_size_overlap":
        # Overlap default là 50 hoặc 100
        overlap = min(100, chunk_size // 5)
        return chunk_fixed_size(markdown_result, chunk_size, overlap)

    if chunk_strategy == "sentence":
        return chunk_by_sentence(markdown_result, chunk_size)

    if chunk_strategy != "paragraph":
        raise HTTPException(status_code=400, detail=f"Khong ho tro chunk_strategy {chunk_strategy}")

    paragraphs = markdown_result.split("\n\n")
    current_chunk = ""
    for paragraph in paragraphs:
        if len(current_chunk) + len(paragraph) < chunk_size:
            current_chunk += paragraph + "\n\n"
        else:
            if current_chunk.strip():
                chunks.append(current_chunk.strip())
            current_chunk = paragraph + "\n\n"

    if current_chunk.strip():
        chunks.append(current_chunk.strip())

    return chunks


@app.post("/api/parse-document")
async def parse_and_process_document(
    file: UploadFile = File(...),
    model_name: str = Form("bge-m3"),
    chunk_strategy: str = Form("markdown_header"),
    chunk_size: int = Form(500),
):
    if model_name not in SUPPORTED_MODELS:
        raise HTTPException(status_code=400, detail="Mo hinh khong duoc ho tro")

    safe_filename = os.path.basename(file.filename or "uploaded_file")
    temp_dir = "temp_files"
    os.makedirs(temp_dir, exist_ok=True)
    temp_filename = f"{uuid.uuid4().hex}_{safe_filename}"
    file_path = os.path.join(temp_dir, temp_filename)

    try:
        with open(file_path, "wb") as buffer:
            shutil.copyfileobj(file.file, buffer)

        extension = os.path.splitext(safe_filename)[1].lower()
        if extension == ".pdf":
            markdown_result = parse_pdf_to_markdown(file_path)
        elif extension in [".docx", ".doc"]:
            markdown_result = parse_docx_to_markdown(file_path)
        elif extension in [".pptx", ".ppt"]:
            markdown_result = parse_pptx_to_markdown(file_path)
        elif extension in [".xlsx", ".xls"]:
            markdown_result = parse_xlsx_to_markdown(file_path)
        else:
            raise HTTPException(status_code=400, detail=f"Khong ho tro file {extension}")

        chunks = chunk_markdown(markdown_result, chunk_strategy, chunk_size)
        model = get_model(model_name)
        processed_chunks = []
        for index, chunk_text in enumerate(chunks):
            vector = await asyncio.to_thread(model.encode, chunk_text)
            processed_chunks.append({
                "chunk_index": index,
                "content": chunk_text,
                "vector": vector.tolist(),
            })

        return {
            "filename": safe_filename,
            "markdown": markdown_result,
            "total_chunks": len(processed_chunks),
            "chunks": processed_chunks,
        }
    except HTTPException:
        raise
    except Exception as exc:
        raise HTTPException(status_code=500, detail=str(exc)) from exc
    finally:
        if os.path.exists(file_path):
            os.remove(file_path)


class QwenChatRequest(BaseModel):
    message: str


@app.post("/qwen-chat")
async def qwen_chat(request: QwenChatRequest):
    try:
        model, tokenizer = load_qwen_model()
        input_ids = tokenizer.apply_chat_template(
            [{"role": "user", "content": request.message}],
            tokenize=True,
            add_generation_prompt=True,
            return_tensors="pt",
        )
        input_ids = input_ids.to("cuda" if torch.cuda.is_available() else "cpu")
        output_ids = model.generate(
            input_ids=input_ids,
            max_new_tokens=256,
            temperature=0.7,
            do_sample=True,
        )
        answer = tokenizer.decode(
            output_ids[0][input_ids.shape[-1]:],
            skip_special_tokens=True,
        ).strip()
        return {"answer": answer}
    except Exception as exc:
        raise HTTPException(status_code=500, detail=str(exc)) from exc
