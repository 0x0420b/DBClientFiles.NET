﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using DBClientFiles.NET.Parsing.File;
using DBClientFiles.NET.Parsing.File.Records;
using DBClientFiles.NET.Parsing.Reflection;
using DBClientFiles.NET.Parsing.Serialization.Generators;
using DBClientFiles.NET.Utils;
using TypeToken = DBClientFiles.NET.Parsing.Reflection.TypeToken;

namespace DBClientFiles.NET.Parsing.Serialization
{
    internal abstract class StructuredSerializer<T> : ISerializer<T>
    {
        private Expression _keyAccessExpression;

        protected delegate void TypeCloner(in T source, out T target);
        protected delegate void TypeDeserializer(IRecordReader recordReader, IParser<T> fileParser, out T instance);
        protected delegate int TypeKeyGetter(in T source);
        protected delegate void TypeKeySetter(out T source, int key);

        private TypeCloner _cloneMethod;
        private TypeDeserializer _deserializer;
        private TypeKeyGetter _keyGetter;
        private TypeKeySetter _keySetter;

        private StorageOptions _options;
        public ref readonly StorageOptions Options {
            get => ref _options;
        }
        public TypeToken Type { get; protected set; }

        protected abstract TypedSerializerGenerator<T, TypeDeserializer> Generator { get; set; }

        public virtual void Initialize(IBinaryStorageFile storage)
        {
            _options = storage.Options;
            Type = storage.Type;

            SetIndexColumn(storage.Header.IndexColumn);
        }

        public void SetIndexColumn(int indexColumn)
        {
            var rootExpression = Expression.Parameter(typeof(T).MakeByRefType(), "model");
            _keyAccessExpression = rootExpression;

            var indexColumnMember = Type.GetMemberByIndex(ref indexColumn, ref _keyAccessExpression, _options.TokenType);
            if (indexColumnMember == null)
                throw new InvalidOperationException($"Invalid structure: Unable to find an index column.");

            if (indexColumnMember.TypeToken.Type != typeof(int) && indexColumnMember.TypeToken.Type != typeof(uint))
            {
                throw new InvalidOperationException($"Invalid structure: {_keyAccessExpression} is expected to be the index, but its type doesn't match. Needs to be (u)int.");
            }

            { /* key getter */
                _keyGetter = Expression.Lambda<TypeKeyGetter>(
                    // Box to int - unfortunate but necessary (?)
                    Expression.ConvertChecked(_keyAccessExpression, typeof(int)),
                    rootExpression).Compile();
            }

            { /* key setter */
                var paramValue = Expression.Parameter(typeof(int));

                _keySetter = Expression.Lambda<TypeKeySetter>(
                    Expression.Assign(_keyAccessExpression, Expression.ConvertChecked(paramValue, _keyAccessExpression.Type)
                ), rootExpression, paramValue).Compile();
            }
        }

        /// <summary>
        /// Extract the key value of a given record.
        /// </summary>
        /// <param name="instance"></param>
        /// <returns></returns>
        public int GetKey(in T instance) => _keyGetter(in instance);
    
        /// <summary>
        /// Force-set the key of a record to the provided value.
        /// </summary>
        /// <param name="instance">The record instance to modify.</param>
        /// <param name="key">The new key value to set<</param>
        public void SetKey(out T instance, int key) => _keySetter(out instance, key);

        public T Deserialize(IRecordReader reader, IParser<T> parser)
        {
            if (_deserializer == null)
                _deserializer = Generator?.GenerateDeserializer();

            if (_deserializer == null)
                throw new InvalidOperationException("A generator is needed for file parsing.");

            _deserializer.Invoke(reader, parser, out var instance);
            return instance;
        }

        /// <summary>
        /// Clone the provided instance.
        /// </summary>
        /// <param name="origin"></param>
        /// <returns></returns>
        public T Clone(in T origin)
        {
            if (_cloneMethod == null)
            {
                Debug.Assert(Options.MemberType == MemberTypes.Field || Options.MemberType == MemberTypes.Property);

                var oldInstanceParam = Expression.Parameter(typeof(T).MakeByRefType());
                var newInstanceParam = Expression.Parameter(typeof(T).MakeByRefType());

                var body = new List<Expression> {
                    Expression.Assign(newInstanceParam, New<T>.Expression())
                };

                foreach (var memberInfo in Type.Members)
                {
                    var oldMemberAccess = Expression.MakeMemberAccess(oldInstanceParam, memberInfo.MemberInfo);
                    var newMemberAccess = Expression.MakeMemberAccess(newInstanceParam, memberInfo.MemberInfo);

                    body.Add(CloneMember(memberInfo, oldMemberAccess, newMemberAccess));
                }

                body.Add(newInstanceParam);

                var bodyBlock = Expression.Block(body);
                _cloneMethod = Expression.Lambda<TypeCloner>(bodyBlock, oldInstanceParam, newInstanceParam).Compile();
            }

            _cloneMethod.Invoke(in origin, out var instance);
            return instance;
        }

        private Expression CloneMember(MemberToken memberInfo, Expression oldMember, Expression newMember)
        {
            if (memberInfo.IsArray)
            {
                var sizeVarExpr = Expression.Variable(typeof(int));
                var lengthValue = Expression.MakeMemberAccess(oldMember,
                    oldMember.Type.GetProperty("Length", BindingFlags.Public | BindingFlags.Instance));
                var newArrayExpr = Expression.NewArrayBounds(memberInfo.TypeToken.GetElementTypeToken().Type, sizeVarExpr);

                var loopItr = Expression.Variable(typeof(int));
                var loopCondition = Expression.LessThan(loopItr, sizeVarExpr);
                var loopExitLabel = Expression.Label();

                return Expression.Block(new[] { loopItr, sizeVarExpr },
                    Expression.Assign(sizeVarExpr, lengthValue),
                    Expression.Assign(newMember, newArrayExpr),
                    Expression.Assign(loopItr, Expression.Constant(0)),
                    Expression.Loop(
                        Expression.IfThenElse(loopCondition,
                            Expression.Block(
                                CloneMember(memberInfo, Expression.ArrayAccess(oldMember, loopItr), Expression.ArrayAccess(newMember, loopItr)),
                                Expression.PreIncrementAssign(loopItr)
                            ),
                            Expression.Break(loopExitLabel)
                        ), loopExitLabel));
            }


            var typeInfo = Type.GetChildToken(oldMember.Type);

            if (typeInfo.Type == typeof(string) || typeInfo.Type.IsPrimitive)
                return Expression.Assign(newMember, oldMember);

            var block = new List<Expression>() {
                Expression.Assign(newMember, New.Expression(newMember.Type))
            };

            foreach (var childInfo in typeInfo.Members)
            {
                if (Options.TokenType != childInfo.MemberType || childInfo.IsReadOnly)
                    continue;

                var oldChild = Expression.MakeMemberAccess(oldMember, childInfo.MemberInfo);
                var newChild = Expression.MakeMemberAccess(newMember, childInfo.MemberInfo);

                block.Add(CloneMember(childInfo, oldChild, newChild));
            }

            return block.Count == 1
                ? (Expression)Expression.Assign(newMember, oldMember)
                : (Expression)Expression.Block(block);
        }
    }
}
