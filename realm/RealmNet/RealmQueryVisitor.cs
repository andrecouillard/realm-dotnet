using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using RealmNet.Interop;

namespace RealmNet
{
    public class RealmQueryVisitor : ExpressionVisitor
    {
        private Realm _realm;
        private ICoreProvider _coreProvider;
        private IQueryHandle _coreQueryHandle;

        public IEnumerable Process(Realm realm, ICoreProvider coreProvider, Expression expression, Type returnType)
        {
            _realm = realm;
            _coreProvider = coreProvider;

            Visit(expression);

            var innerType = returnType.GenericTypeArguments[0];
            var list = Activator.CreateInstance(typeof(List<>).MakeGenericType(innerType));
            var add = list.GetType().GetTypeInfo().GetDeclaredMethod("Add");

            var indices = _coreProvider.ExecuteQuery(_coreQueryHandle, innerType);
            foreach (var rowIndex in indices)
            {
                var o = Activator.CreateInstance(innerType);
                ((RealmObject)o)._Manage(_realm, _coreProvider, rowIndex);
                add.Invoke(list, new[] { o });
            }
            return (IEnumerable)list;
        }

        private static Expression StripQuotes(Expression e)
        {
            while (e.NodeType == ExpressionType.Quote)
            {
                e = ((UnaryExpression)e).Operand;
            }
            return e;
        }

        protected override Expression VisitMethodCall(MethodCallExpression m)
        {
            if (m.Method.DeclaringType == typeof(Queryable) && m.Method.Name == "Where")
            {
                this.Visit(m.Arguments[0]);

                LambdaExpression lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
                this.Visit(lambda.Body);
                return m;
            }
            throw new NotSupportedException(string.Format("The method '{0}' is not supported", m.Method.Name));
        }

        protected override Expression VisitUnary(UnaryExpression u)
        {
            switch (u.NodeType)
            {
                case ExpressionType.Not:
                    this.Visit(u.Operand);
                    break;
                default:
                    throw new NotSupportedException(string.Format("The unary operator '{0}' is not supported", u.NodeType));
            }
            return u;
        }

        protected override Expression VisitBinary(BinaryExpression b)
        {
            this.Visit(b.Left);
            switch (b.NodeType)
            {
                case ExpressionType.And:
                    break;
                case ExpressionType.Or:
                    break;
                case ExpressionType.Equal:
                    _coreProvider.QueryEqual(_coreQueryHandle, ((MemberExpression)b.Left).Member.Name, ((ConstantExpression)b.Right).Value);
                    break;
                case ExpressionType.NotEqual:
                    break;
                case ExpressionType.LessThan:
                    break;
                case ExpressionType.LessThanOrEqual:
                    break;
                case ExpressionType.GreaterThan:
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    break;
                default:
                    throw new NotSupportedException(string.Format("The binary operator '{0}' is not supported", b.NodeType));
            }
            this.Visit(b.Right);
            return b;
        }

        protected override Expression VisitConstant(ConstantExpression c)
        {
            IQueryable q = c.Value as IQueryable;
            if (q != null)
            {
                // assume constant nodes w/ IQueryables are table references
                if (_coreQueryHandle != null)
                    throw new Exception("We already have a table...");

                var tableName = q.ElementType.Name;
                _coreQueryHandle = _coreProvider.CreateQuery(_realm.TransactionGroupHandle, tableName);
            }
            else if (c.Value == null)
            {
            }
            else
            {
                if (c.Value is bool)
                {
                } 
                else if (c.Value is string)
                {
                }
                else if (c.Value.GetType() == typeof (object))
                {
                    throw new NotSupportedException(string.Format("The constant for '{0}' is not supported", c.Value));
                }
                else
                {
                }
            }
            return c;
        }

        protected  Expression VisitMemberAccess(MemberExpression m)
        {
            if (m.Expression != null && m.Expression.NodeType == ExpressionType.Parameter)
            {
                return m;
            }
            throw new NotSupportedException(string.Format("The member '{0}' is not supported", m.Member.Name));
        }
    }
}